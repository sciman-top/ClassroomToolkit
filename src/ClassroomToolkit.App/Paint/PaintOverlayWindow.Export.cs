using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ClassroomToolkit.App.Ink;
using MessageBox = System.Windows.MessageBox;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// Ink export and persistence integration for PaintOverlayWindow.
/// Handles Method A (sidecar save/load) and Method B (composite image export).
/// </summary>
public partial class PaintOverlayWindow
{
    private InkPersistenceService? _inkPersistence;
    private InkExportService? _inkExport;
    private InkExportOptions _inkExportOptions = new();
    private sealed record SidecarPersistSnapshot(
        InkPersistenceService Persistence,
        string SourcePath,
        int PageIndex,
        List<InkStrokeData> Strokes,
        string SnapshotHash);

    /// <summary>
    /// Inject the persistence and export services. Must be called after construction.
    /// </summary>
    public void SetInkPersistenceServices(InkPersistenceService persistence, InkExportService export, InkExportOptions? exportOptions = null)
    {
        _inkPersistence = persistence;
        _inkExport = export;
        if (exportOptions != null)
        {
            _inkExportOptions = exportOptions;
        }
    }

    /// <summary>
    /// Save current ink strokes to sidecar file (Method A).
    /// Called on page navigation, file close, and app exit.
    /// </summary>
    private void PersistInkToSidecar(
        List<InkStrokeData>? preparedStrokes = null,
        string? preparedSourcePath = null,
        int? preparedPageIndex = null)
    {
        _ = TryPersistInkToSidecarStrict(preparedStrokes, preparedSourcePath, preparedPageIndex, out _);
    }

    private bool TryPersistInkToSidecarStrict(
        List<InkStrokeData>? preparedStrokes,
        string? preparedSourcePath,
        int? preparedPageIndex,
        out string? errorMessage)
    {
        errorMessage = null;
        var persistence = _inkPersistence;
        if (persistence == null || !_inkSaveEnabled)
        {
            return true;
        }
        var sourcePath = string.IsNullOrWhiteSpace(preparedSourcePath) ? _currentDocumentPath : preparedSourcePath;
        var pageIndex = preparedPageIndex ?? _currentPageIndex;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return true;
        }
        try
        {
            var strokes = preparedStrokes;
            if (strokes == null)
            {
                FinalizeActiveInkOperation();
                strokes = CloneCommittedInkStrokes();
            }
            var hash = ComputeInkHash(strokes);
            persistence.SaveInkForFile(sourcePath, pageIndex, strokes);
            var persistedStrokes = persistence.LoadInkPageForFile(sourcePath, pageIndex) ?? new List<InkStrokeData>();
            var persistedHash = ComputeInkHash(persistedStrokes);
            if (!string.Equals(hash, persistedHash, StringComparison.Ordinal))
            {
                errorMessage = $"hash-mismatch expected={hash} actual={persistedHash}";
                TrackInkWalSnapshot(sourcePath, pageIndex, strokes, hash);
                return false;
            }
            _inkDirtyPages.MarkPersistedIfUnchanged(sourcePath, pageIndex, hash);
            ClearInkWalSnapshot(sourcePath, pageIndex);
            if (strokes.Count == 0 && _inkExport != null)
            {
                _inkExport.RemoveCompositeOutputsForPage(sourcePath, pageIndex);
            }
            _inkDiagnostics?.OnSyncPersist();
            System.Diagnostics.Debug.WriteLine($"[InkPersist] Saved {strokes.Count} strokes for page {pageIndex} of {sourcePath}");
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            var fallbackStrokes = preparedStrokes ?? new List<InkStrokeData>();
            TrackInkWalSnapshot(sourcePath, pageIndex, fallbackStrokes, ComputeInkHash(fallbackStrokes));
            System.Diagnostics.Debug.WriteLine($"[InkPersist] Save failed: {ex.Message}");
            return false;
        }
    }

    private void ScheduleSidecarAutoSave()
    {
        if (!_inkSaveEnabled || _inkSidecarAutoSaveTimer == null)
        {
            return;
        }
        _inkSidecarAutoSaveTimer.Stop();
        _inkSidecarAutoSaveTimer.Start();
    }

    private bool TryCaptureSidecarPersistSnapshot(bool requireDirty, out SidecarPersistSnapshot? snapshot)
    {
        snapshot = null;
        var persistence = _inkPersistence;
        if (persistence == null || !_inkSaveEnabled)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return false;
        }
        if (requireDirty && !IsCurrentPageDirty())
        {
            return false;
        }

        // Never finalize/capture while user is actively drawing/erasing.
        // Otherwise auto-save may truncate the in-flight stroke and cause visible "missing ink".
        if (IsInkOperationActive())
        {
            _inkDiagnostics?.OnAutoSaveDeferred("capture-active-operation");
            return false;
        }

        FinalizeActiveInkOperation();
        snapshot = new SidecarPersistSnapshot(
            persistence,
            _currentDocumentPath,
            _currentPageIndex,
            CloneCommittedInkStrokes(),
            ComputeInkHash(_inkStrokes));
        return true;
    }

    private void QueueSidecarAutoSave(SidecarPersistSnapshot snapshot)
    {
        var generation = _inkSidecarAutoSaveGate.NextGeneration();
        _ = _inkSidecarAutoSaveGate.RunAsync(generation, async isCurrent =>
        {
            for (int attempt = 1; attempt <= InkSidecarAutoSaveRetryMax; attempt++)
            {
                if (!isCurrent())
                {
                    return;
                }

                if (TryPersistSidecarSnapshot(snapshot, logFailure: attempt == InkSidecarAutoSaveRetryMax))
                {
                    _ = Dispatcher.BeginInvoke(() =>
                    {
                        var persisted = _inkDirtyPages.MarkPersistedIfUnchanged(
                            snapshot.SourcePath,
                            snapshot.PageIndex,
                            snapshot.SnapshotHash);
                        _inkDiagnostics?.OnAutoSavePersistResult(persisted);
                    });
                    return;
                }

                if (attempt >= InkSidecarAutoSaveRetryMax)
                {
                    break;
                }

                try
                {
                    await Task.Delay(InkSidecarAutoSaveRetryDelayMs * attempt).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
            }

            if (!isCurrent())
            {
                return;
            }

            _ = Dispatcher.BeginInvoke(() =>
            {
                _inkDirtyPages.MarkModified(snapshot.SourcePath, snapshot.PageIndex, snapshot.SnapshotHash);
                _inkDiagnostics?.OnAutoSaveFailure();
                ScheduleSidecarAutoSave();
            });
        });
    }

    private bool TryPersistSidecarSnapshot(SidecarPersistSnapshot snapshot, bool logFailure)
    {
        try
        {
            snapshot.Persistence.SaveInkForFile(snapshot.SourcePath, snapshot.PageIndex, snapshot.Strokes);
            if (snapshot.Strokes.Count == 0 && _inkExport != null)
            {
                _inkExport.RemoveCompositeOutputsForPage(snapshot.SourcePath, snapshot.PageIndex);
            }
            return true;
        }
        catch (Exception ex)
        {
            if (logFailure)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[InkPersist] Auto-save failed after retries: source={snapshot.SourcePath}, page={snapshot.PageIndex}, error={ex.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Try to load ink from sidecar for the current page (Method A fallback).
    /// Returns true if strokes were loaded from disk.
    /// </summary>
    private bool TryLoadInkFromSidecar()
    {
        if (!TryLoadSidecarPageInkToCache(_currentDocumentPath, _currentPageIndex, out var strokes))
        {
            return false;
        }
        ApplyInkStrokes(strokes);
        System.Diagnostics.Debug.WriteLine($"[InkPersist] Loaded {strokes.Count} strokes from sidecar for page {_currentPageIndex}");
        return true;
    }

    /// <summary>
    /// Check whether sidecar ink exists for the given file and prompt user.
    /// Returns true if user chose to load ink.
    /// </summary>
    internal bool PromptAndLoadSidecarInk(string filePath)
    {
        if (_inkPersistence == null || !_inkPersistence.HasInkForFile(filePath))
        {
            return false;
        }

        var result = MessageBox.Show(
            "检测到此文件有保存的笔迹，是否加载？",
            "加载笔迹",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return false;
        }

        try
        {
            var pageIndex = _photoDocumentIsPdf ? _currentPageIndex : 1;
            if (TryLoadSidecarPageInkToCache(filePath, pageIndex, out var currentPageStrokes))
            {
                ApplyInkStrokes(currentPageStrokes);
            }
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InkPersist] PromptAndLoad failed: {ex.Message}");
            return false;
        }
    }

    private bool TryLoadSidecarPageInkToCache(string sourcePath, int pageIndex, out List<InkStrokeData> strokes)
    {
        strokes = new List<InkStrokeData>();
        if (_inkPersistence == null || string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        try
        {
            var loaded = _inkPersistence.LoadInkPageForFile(sourcePath, pageIndex);
            if (loaded == null || loaded.Count == 0)
            {
                return false;
            }

            strokes = CloneInkStrokes(loaded);
            var cacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, IsPdfFile(sourcePath));
            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                _photoCache.Set(cacheKey, strokes);
            }
            MarkInkPageLoaded(sourcePath, pageIndex, strokes);
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InkPersist] Load page failed: source={sourcePath}, page={pageIndex}, error={ex.Message}");
            return false;
        }
    }

    private bool TryLoadNeighborInkFromSidecarIntoCache(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return TryLoadSidecarPageInkToCache(_currentDocumentPath, pageIndex, out _);
        }

        var sequenceIndex = pageIndex - 1;
        if (sequenceIndex < 0 || sequenceIndex >= _photoSequencePaths.Count)
        {
            return false;
        }

        var sourcePath = _photoSequencePaths[sequenceIndex];
        return TryLoadSidecarPageInkToCache(sourcePath, 1, out _);
    }

    /// <summary>
    /// Handler for the export button click on both sidebars.
    /// </summary>
    private void OnExportInkClick(object sender, RoutedEventArgs e)
    {
        if (_inkPersistence == null || _inkExport == null)
        {
            MessageBox.Show("笔迹服务未初始化。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            MessageBox.Show("当前没有打开的文件。", "导出失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sourceDir = Path.GetDirectoryName(_currentDocumentPath) ?? string.Empty;
        ExportDirectory(sourceDir);
    }

    private void ExportCurrentFile()
    {
        if (_inkPersistence == null || _inkExport == null)
        {
            return;
        }

        if (!FlushDirtyInkPagesToSidecarForExport(directoryPath: null))
        {
            return;
        }
        var inkDoc = BuildInkDocumentForExport(_currentDocumentPath);
        if (!InkExportSnapshotBuilder.HasAnyInkStrokes(inkDoc))
        {
            MessageBox.Show("当前文件没有笔迹，无法导出。", "导出笔迹合成图片", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        // Check overwrite
        var existing = _inkExport.GetExistingOutputPaths(_currentDocumentPath, inkDoc, _inkExportOptions);
        if (existing.Count > 0)
        {
            var result = MessageBox.Show(
                $"导出目录中已存在 {existing.Count} 个同名文件，是否覆盖？",
                "确认覆盖",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
        }

        var exportResult = _inkExport.ExportAllPagesForFileDetailed(_currentDocumentPath, inkDoc, _inkExportOptions);
        var outputs = exportResult.OutputPaths;
        if (outputs.Count > 0)
        {
            MessageBox.Show(
                $"导出完成：新增 {exportResult.ExportedCount}，跳过 {exportResult.SkippedCount}，失败 {exportResult.FailedCount}\n输出文件 {outputs.Count} 个到：\n{InkExportService.GetExportDirectory(_currentDocumentPath)}",
                "导出完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            MessageBox.Show("没有可导出的内容。", "导出", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void ExportDirectory(string directoryPath)
    {
        if (_inkExport == null || _inkPersistence == null)
        {
            return;
        }

        // Freeze any in-progress pointer action before building export snapshots.
        FinalizeActiveInkOperation();
        if (!FlushDirtyInkPagesToSidecarForExport(directoryPath))
        {
            return;
        }

        var filesToExportSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_inkExportOptions.Scope == InkExportScope.SessionChangesOnly)
        {
            foreach (var sourcePath in EnumerateSessionModifiedSourcesInDirectory(directoryPath))
            {
                filesToExportSet.Add(sourcePath);
            }
        }
        else
        {
            foreach (var file in _inkPersistence.ListFilesWithInk(directoryPath))
            {
                filesToExportSet.Add(file);
            }
            foreach (var file in _inkExport.ListFilesWithCompositeExports(directoryPath))
            {
                filesToExportSet.Add(file);
            }

            if (!_inkSaveEnabled)
            {
                foreach (var sourcePath in EnumerateCachedInkSourcesInDirectory(directoryPath))
                {
                    filesToExportSet.Add(sourcePath);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_currentDocumentPath) &&
            string.Equals(Path.GetDirectoryName(_currentDocumentPath), directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            var currentInkDoc = BuildInkDocumentForExport(_currentDocumentPath);
            if (InkExportSnapshotBuilder.HasAnyInkStrokes(currentInkDoc)
                || IsPageDirtyForExportOverlay(_currentDocumentPath, _currentPageIndex))
            {
                filesToExportSet.Add(_currentDocumentPath);
            }
        }

        var filesToExport = filesToExportSet.ToList();
        if (filesToExport.Count == 0)
        {
            MessageBox.Show("当前目录没有可导出的笔迹合成记录。", "导出", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var inkDocsForExport = new Dictionary<string, InkDocumentData?>(StringComparer.OrdinalIgnoreCase);
        var overwriteCount = 0;
        foreach (var sourcePath in filesToExport)
        {
            var inkDoc = BuildInkDocumentForExport(sourcePath);
            inkDocsForExport[sourcePath] = inkDoc;
            if (!InkExportSnapshotBuilder.HasAnyInkStrokes(inkDoc))
            {
                continue;
            }

            overwriteCount += _inkExport.GetExistingOutputPaths(sourcePath, inkDoc, _inkExportOptions).Count;
        }
        var exportQueue = filesToExport
            .OrderByDescending(path => EstimateExportWorkload(path, inkDocsForExport.TryGetValue(path, out var doc) ? doc : null))
            .ToList();

        // Snapshot export payloads so background work is isolated from subsequent edits.
        var exportSnapshotDocs = new Dictionary<string, InkDocumentData?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in inkDocsForExport)
        {
            exportSnapshotDocs[pair.Key] = InkExportSnapshotBuilder.CloneDocument(pair.Value, CloneInkStrokes);
        }

        if (overwriteCount > 0)
        {
            var overwriteChoice = MessageBox.Show(
                $"导出目录中已存在 {overwriteCount} 个同名文件，是否覆盖？",
                "确认覆盖",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (overwriteChoice != MessageBoxResult.Yes)
            {
                return;
            }
        }

        // Run export on background thread with progress
        var progressWindow = new Window
        {
            Title = "正在导出...",
            Width = 400,
            Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            WindowStyle = WindowStyle.ToolWindow,
            Topmost = true
        };
        var progressText = new System.Windows.Controls.TextBlock
        {
            Text = "准备中...",
            Margin = new Thickness(16),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        progressWindow.Content = progressText;
        progressWindow.Show();

        var exportOptions = _inkExportOptions;
        var exportService = _inkExport;

        Task.Run(() =>
        {
            var outputs = new ConcurrentBag<string>();
            var exportedCount = 0;
            var skippedCount = 0;
            var failedCount = 0;
            var total = exportQueue.Count;
            var completed = 0;
            var adaptiveParallel = Math.Max(1, Math.Min(4, Math.Max(1, Environment.ProcessorCount / 2)));
            var configuredParallel = exportOptions.MaxParallelFiles > 0 ? exportOptions.MaxParallelFiles : adaptiveParallel;
            var maxParallel = Math.Max(1, Math.Min(configuredParallel, Math.Max(1, total)));
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = maxParallel
            };

            Parallel.ForEach(exportQueue, parallelOptions, sourcePath =>
            {
                var inkDoc = exportSnapshotDocs.TryGetValue(sourcePath, out var mapped) ? mapped : null;
                var fileResult = exportService.ExportAllPagesForFileDetailed(sourcePath, inkDoc, exportOptions);
                LogExportAudit(sourcePath, inkDoc);
                foreach (var output in fileResult.OutputPaths)
                {
                    outputs.Add(output);
                }
                Interlocked.Add(ref exportedCount, fileResult.ExportedCount);
                Interlocked.Add(ref skippedCount, fileResult.SkippedCount);
                Interlocked.Add(ref failedCount, fileResult.FailedCount);

                var done = Interlocked.Increment(ref completed);
                _ = Dispatcher.BeginInvoke(() =>
                {
                    progressText.Text = $"正在导出 ({done}/{total}, 并发 {maxParallel}): {Path.GetFileName(sourcePath)}";
                });
            });

            Dispatcher.BeginInvoke(() =>
            {
                progressWindow.Close();
                MessageBox.Show(
                    $"导出完成：新增 {exportedCount}，跳过 {skippedCount}，失败 {failedCount}\n输出文件 {outputs.Count} 个。",
                    "导出完成",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            });
        });
    }

    private static long EstimateExportWorkload(string sourcePath, InkDocumentData? inkDoc)
    {
        var hasInkPages = inkDoc?.Pages?.Count(page => page.Strokes?.Count > 0) ?? 0;
        var pageWeight = Math.Max(1, hasInkPages);
        long sizeWeight = 1;
        try
        {
            if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
            {
                sizeWeight = Math.Max(1, new FileInfo(sourcePath).Length / (512 * 1024));
            }
        }
        catch
        {
            sizeWeight = 1;
        }
        return pageWeight * sizeWeight;
    }

    private InkDocumentData? BuildInkDocumentForExport(string sourceFilePath)
    {
        if (_inkPersistence == null || string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return null;
        }

        var inkDoc = _inkPersistence.LoadInkForFile(sourceFilePath) ?? new InkDocumentData
        {
            SourcePath = sourceFilePath
        };

        var includeRuntimeCachePages = !_inkSaveEnabled;
        if (includeRuntimeCachePages)
        {
            InkExportSnapshotBuilder.MergeCachedPages(inkDoc, sourceFilePath, _photoCache.Snapshot(), CloneInkStrokes);
        }

        if (!string.Equals(sourceFilePath, _currentDocumentPath, StringComparison.OrdinalIgnoreCase))
        {
            ApplyExportScopeFilter(inkDoc);
            return inkDoc;
        }

        BackfillStrokeReferenceSizeForCurrentImage(inkDoc);
        if (includeRuntimeCachePages || IsPageDirtyForExportOverlay(sourceFilePath, _currentPageIndex))
        {
            FinalizeActiveInkOperation();
            InkExportSnapshotBuilder.UpsertPageStrokes(inkDoc, sourceFilePath, _currentPageIndex, CloneCommittedInkStrokes());
        }
        ApplyExportScopeFilter(inkDoc);
        return inkDoc;
    }

    private bool IsPageDirtyForExportOverlay(string sourcePath, int pageIndex)
    {
        return _inkDirtyPages.IsDirty(sourcePath, pageIndex);
    }

    private bool FlushDirtyInkPagesToSidecarForExport(string? directoryPath)
    {
        var flushResult = InkDirtyPageFlushCoordinator.Flush(
            inkSaveEnabled: _inkSaveEnabled && _inkPersistence != null,
            directoryPath: directoryPath,
            stopAutoSaveTimer: () => _inkSidecarAutoSaveTimer?.Stop(),
            cancelAutoSaveGeneration: () => _inkSidecarAutoSaveGate.NextGeneration(),
            finalizeActiveOperation: FinalizeActiveInkOperation,
            getDirtyPages: _inkDirtyPages.GetDirtyPages,
            tryGetPageStrokes: TryGetRuntimePageStrokesForPersist,
            persistPage: (string sourcePath, int pageIndex, List<InkStrokeData> strokes, out string? error) =>
            {
                return TryPersistInkToSidecarStrict(strokes, sourcePath, pageIndex, out error);
            });

        if (flushResult.IsSuccess)
        {
            return true;
        }

        var topFailures = flushResult.Failures
            .Take(5)
            .Select(item => $"{Path.GetFileName(item.SourcePath)} 第 {item.PageIndex} 页：{item.Error}")
            .ToList();
        var details = string.Join("\n", topFailures);
        MessageBox.Show(
            $"导出前自动保存校验失败，已中止导出。\n失败 {flushResult.Failures.Count} 页：\n{details}",
            "导出已中止",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return false;
    }

    private void LogExportAudit(string sourcePath, InkDocumentData? inkDoc)
    {
        if (inkDoc?.Pages == null || inkDoc.Pages.Count == 0)
        {
            return;
        }

        foreach (var page in inkDoc.Pages.Where(p => p.PageIndex > 0))
        {
            var hash = ComputeInkHash(page.Strokes ?? new List<InkStrokeData>());
            var dirty = _inkDirtyPages.IsDirty(sourcePath, page.PageIndex);
            _inkDirtyPages.TryGetRuntimeState(sourcePath, page.PageIndex, out var version, out var lastKnownHash, out _);
            System.Diagnostics.Debug.WriteLine(
                $"[InkExportAudit] source={sourcePath}, page={page.PageIndex}, version={version}, dirty={dirty}, expectedHash={hash}, runtimeHash={lastKnownHash}");
        }
    }

    private bool TryGetRuntimePageStrokesForPersist(string sourcePath, int pageIndex, out List<InkStrokeData> strokes)
    {
        strokes = new List<InkStrokeData>();
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        if (string.Equals(sourcePath, _currentDocumentPath, StringComparison.OrdinalIgnoreCase) && pageIndex == _currentPageIndex)
        {
            strokes = CloneCommittedInkStrokes();
            return true;
        }

        var cacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, IsPdfFile(sourcePath));
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        if (_photoCache.TryGet(cacheKey, out var cached))
        {
            strokes = CloneInkStrokes(cached);
            return true;
        }

        return false;
    }

    private void BackfillStrokeReferenceSizeForCurrentImage(InkDocumentData inkDoc)
    {
        if (_photoDocumentIsPdf || PhotoBackground.Source is not System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            return;
        }

        var width = GetBitmapDisplayWidthInDip(bitmap);
        var height = GetBitmapDisplayHeightInDip(bitmap);
        if (width <= 0.5 || height <= 0.5)
        {
            return;
        }

        foreach (var page in inkDoc.Pages)
        {
            foreach (var stroke in page.Strokes)
            {
                if (stroke.ReferenceWidth > 0.5 && stroke.ReferenceHeight > 0.5)
                {
                    continue;
                }
                stroke.ReferenceWidth = width;
                stroke.ReferenceHeight = height;
            }
        }
    }

    private void ApplyExportScopeFilter(InkDocumentData inkDoc)
    {
        InkExportSnapshotBuilder.ApplyScopeFilter(
            inkDoc,
            _inkExportOptions.Scope,
            page => WasPageModifiedInSession(inkDoc.SourcePath, page.PageIndex));
    }

    private IEnumerable<string> EnumerateCachedInkSourcesInDirectory(string directoryPath)
    {
        return InkExportSnapshotBuilder.EnumerateCachedInkSourcesInDirectory(_photoCache.Snapshot(), directoryPath);
    }

    // Compatibility shim for existing reflection-based tests/utilities.
    private static bool TryParseCacheKey(string cacheKey, out string sourcePath, out int pageIndex)
    {
        return InkExportSnapshotBuilder.TryParseCacheKey(cacheKey, out sourcePath, out pageIndex);
    }
}
