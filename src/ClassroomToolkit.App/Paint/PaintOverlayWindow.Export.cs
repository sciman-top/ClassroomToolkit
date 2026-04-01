using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using MessageBox = System.Windows.MessageBox;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// Ink export and persistence integration for PaintOverlayWindow.
/// Handles Method A (sidecar save/load) and Method B (composite image export).
/// </summary>
public partial class PaintOverlayWindow
{
    private static readonly JsonSerializerOptions InkHistoryJsonOptions = CreateInkHistoryJsonOptions();
    private InkPersistenceService? _inkPersistence;
    private ClassroomToolkit.Infra.Storage.InkHistorySqliteStoreAdapter? _inkHistorySqliteAdapter;
    private InkExportService? _inkExport;
    private InkExportOptions _inkExportOptions = new();
    private sealed record SidecarPersistSnapshot(
        InkPersistenceService Persistence,
        string SourcePath,
        int PageIndex,
        List<InkStrokeData> Strokes,
        string SnapshotHash);

    private void ShowExportMessageSafe(
        string operation,
        string message,
        string title,
        MessageBoxImage image)
    {
        SafeActionExecutionExecutor.TryExecute(
            () => MessageBox.Show(this, message, title, MessageBoxButton.OK, image),
            ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport][{operation}] messagebox failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private MessageBoxResult ShowExportConfirmationSafe(
        string operation,
        string message,
        string title,
        MessageBoxButton buttons,
        MessageBoxImage image,
        MessageBoxResult fallback = MessageBoxResult.Cancel)
    {
        var result = fallback;
        SafeActionExecutionExecutor.TryExecute(
            () => result = MessageBox.Show(this, message, title, buttons, image),
            ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport][{operation}] confirm failed: {ex.GetType().Name} - {ex.Message}"));
        return result;
    }

    private void DispatchExportUiUpdate(string operation, Action action)
    {
        var scheduled = TryBeginInvoke(action, System.Windows.Threading.DispatcherPriority.Background);
        if (scheduled)
        {
            return;
        }

        if (Dispatcher.CheckAccess())
        {
            SafeActionExecutionExecutor.TryExecute(
                action,
                ex => System.Diagnostics.Debug.WriteLine(
                    $"[InkExport][{operation}] inline-ui-update failed: {ex.GetType().Name} - {ex.Message}"));
            return;
        }

        System.Diagnostics.Debug.WriteLine($"[InkExport][{operation}] ui-update dispatch failed");
    }

    /// <summary>
    /// Inject the persistence and export services. Must be called after construction.
    /// </summary>
    public void SetInkPersistenceServices(
        InkPersistenceService persistence,
        InkExportService export,
        InkExportOptions? exportOptions = null,
        ClassroomToolkit.Infra.Storage.InkHistorySqliteStoreAdapter? inkHistorySqliteAdapter = null)
    {
        ArgumentNullException.ThrowIfNull(persistence);
        ArgumentNullException.ThrowIfNull(export);

        _inkPersistence = persistence;
        _inkHistorySqliteAdapter = inkHistorySqliteAdapter;
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
        string? localError = null;
        var persisted = SafeActionExecutionExecutor.TryExecute(
            () =>
        {
            var strokes = preparedStrokes;
            if (strokes == null)
            {
                FinalizeActiveInkOperation();
                strokes = CloneCommittedInkStrokes();
            }
            var hash = ComputeInkHash(strokes);
            PersistInkHistorySnapshot(sourcePath, pageIndex, strokes, persistence);
            var persistedStrokes = LoadInkHistorySnapshot(sourcePath, pageIndex, persistence);
            var persistedHash = ComputeInkHash(persistedStrokes);
            if (!string.Equals(hash, persistedHash, StringComparison.Ordinal))
            {
                localError = $"hash-mismatch expected={hash} actual={persistedHash}";
                TrackInkWalSnapshot(sourcePath, pageIndex, strokes, hash);
                return false;
            }
            MarkInkPagePersistedIfUnchanged(sourcePath, pageIndex, hash);
            if (strokes.Count == 0 && _inkExport != null)
            {
                _inkExport.RemoveCompositeOutputsForPage(sourcePath, pageIndex);
            }
            PurgePersistedInkForHiddenSourceIfNeeded(sourcePath);
            _inkDiagnostics?.OnSyncPersist();
            System.Diagnostics.Debug.WriteLine($"[InkPersist] Saved {strokes.Count} strokes for page {pageIndex} of {sourcePath}");
            return true;
        },
            fallback: false,
            onFailure: ex =>
            {
                localError = ex.Message;
                var fallbackStrokes = preparedStrokes ?? new List<InkStrokeData>();
                TrackInkWalSnapshot(sourcePath, pageIndex, fallbackStrokes, ComputeInkHash(fallbackStrokes));
                System.Diagnostics.Debug.WriteLine($"[InkPersist] Save failed: {ex.Message}");
            });
        errorMessage = localError;
        return persisted;
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
            if (!_inkSaveEnabled)
            {
                return;
            }

            for (int attempt = 1; attempt <= InkSidecarAutoSaveRetryMax; attempt++)
            {
                if (!isCurrent() || !_inkSaveEnabled)
                {
                    return;
                }

                var runtimeStateKnown = _inkDirtyPages.TryGetRuntimeState(
                    snapshot.SourcePath,
                    snapshot.PageIndex,
                    out _,
                    out var runtimeHash,
                    out _);
                if (!InkAutoSaveSnapshotAdmissionPolicy.ShouldPersistSnapshot(
                        runtimeStateKnown,
                        runtimeHash,
                        snapshot.SnapshotHash))
                {
                    _inkDiagnostics?.OnAutoSaveDeferred("stale-runtime-snapshot");
                    return;
                }

                if (TryPersistSidecarSnapshot(snapshot, logFailure: attempt == InkSidecarAutoSaveRetryMax))
                {
                    DispatchExportUiUpdate("autosave-persisted", () =>
                    {
                        var persisted = MarkInkPagePersistedIfUnchanged(
                            snapshot.SourcePath,
                            snapshot.PageIndex,
                            snapshot.SnapshotHash);
                        PurgePersistedInkForHiddenSourceIfNeeded(snapshot.SourcePath);
                        _inkDiagnostics?.OnAutoSavePersistResult(persisted);
                    });
                    return;
                }

                if (attempt >= InkSidecarAutoSaveRetryMax)
                {
                    break;
                }

                if (!await TryDelayAutoSaveRetryAsync(InkSidecarAutoSaveRetryDelayMs * attempt).ConfigureAwait(false))
                {
                    return;
                }
            }

            if (!isCurrent() || !_inkSaveEnabled)
            {
                return;
            }

            DispatchExportUiUpdate("autosave-failed-reschedule", () =>
            {
                MarkInkPageModified(
                    snapshot.SourcePath,
                    snapshot.PageIndex,
                    snapshot.SnapshotHash,
                    snapshot.Strokes);
                _inkDiagnostics?.OnAutoSaveFailure();
                ScheduleSidecarAutoSave();
            });
        });
    }

    private static async Task<bool> TryDelayAutoSaveRetryAsync(int delayMs)
    {
        try
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[InkPersist] Auto-save retry delay interrupted: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private bool TryPersistSidecarSnapshot(SidecarPersistSnapshot snapshot, bool logFailure)
    {
        return SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                PersistInkHistorySnapshot(
                    snapshot.SourcePath,
                    snapshot.PageIndex,
                    snapshot.Strokes,
                    snapshot.Persistence);
                if (snapshot.Strokes.Count == 0 && _inkExport != null)
                {
                    _inkExport.RemoveCompositeOutputsForPage(snapshot.SourcePath, snapshot.PageIndex);
                }

                return true;
            },
            fallback: false,
            onFailure: ex =>
            {
                if (logFailure)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[InkPersist] Auto-save failed after retries: source={snapshot.SourcePath}, page={snapshot.PageIndex}, error={ex.Message}");
                }
            });
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

        var result = ShowExportConfirmationSafe(
            "prompt-load-sidecar-ink",
            "检测到此文件有保存的笔迹，是否加载？",
            "加载笔迹",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
        {
            return false;
        }

        return SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                var pageIndex = _photoDocumentIsPdf ? _currentPageIndex : 1;
                if (TryLoadSidecarPageInkToCache(filePath, pageIndex, out var currentPageStrokes, allowWhenSaveDisabled: true))
                {
                    ApplyInkStrokes(currentPageStrokes);
                }

                return true;
            },
            fallback: false,
            onFailure: ex => System.Diagnostics.Debug.WriteLine($"[InkPersist] PromptAndLoad failed: {ex.Message}"));
    }

    private bool TryLoadSidecarPageInkToCache(
        string sourcePath,
        int pageIndex,
        out List<InkStrokeData> strokes,
        bool allowWhenSaveDisabled = false)
    {
        strokes = new List<InkStrokeData>();
        if (_inkPersistence == null || string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }
        if (!allowWhenSaveDisabled && !_inkSaveEnabled)
        {
            return false;
        }

        var loadResult = SafeActionExecutionExecutor.TryExecute(
            () =>
        {
            var loaded = LoadInkHistorySnapshot(sourcePath, pageIndex, _inkPersistence);
            if (loaded == null || loaded.Count == 0)
            {
                return (Loaded: false, Strokes: new List<InkStrokeData>());
            }

            var clonedStrokes = CloneInkStrokes(loaded);
            var loadedHash = ComputeInkHash(clonedStrokes);
            var runtimeStateKnown = _inkDirtyPages.TryGetRuntimeState(
                sourcePath,
                pageIndex,
                out _,
                out var runtimeHash,
                out var runtimeDirty);
            if (!InkSidecarLoadAdmissionPolicy.ShouldApplyLoadedSnapshot(
                    runtimeStateKnown,
                    runtimeHash,
                    runtimeDirty,
                    loadedHash))
            {
                var rejectedCacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, IsPdfFile(sourcePath));
                if (!string.IsNullOrWhiteSpace(rejectedCacheKey))
                {
                    _photoCache.Remove(rejectedCacheKey);
                    InvalidateNeighborInkCache(rejectedCacheKey);
                }

                System.Diagnostics.Debug.WriteLine(
                    $"[InkPersist] Skip sidecar snapshot due runtime conflict: source={sourcePath}, page={pageIndex}, runtimeHash={runtimeHash}, loadedHash={loadedHash}, dirty={runtimeDirty}");
                return (Loaded: false, Strokes: new List<InkStrokeData>());
            }

            var cacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, IsPdfFile(sourcePath));
            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                _photoCache.Set(cacheKey, clonedStrokes);
            }

            MarkInkPageLoaded(sourcePath, pageIndex, clonedStrokes);
            return (Loaded: true, Strokes: clonedStrokes);
        },
            fallback: (Loaded: false, Strokes: new List<InkStrokeData>()),
            onFailure: ex =>
            {
                System.Diagnostics.Debug.WriteLine($"[InkPersist] Load page failed: source={sourcePath}, page={pageIndex}, error={ex.Message}");
            });
        if (!loadResult.Loaded)
        {
            return false;
        }

        strokes = loadResult.Strokes;
        return true;
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

    private static JsonSerializerOptions CreateInkHistoryJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private void PersistInkHistorySnapshot(
        string sourcePath,
        int pageIndex,
        List<InkStrokeData> strokes,
        InkPersistenceService persistence)
    {
        var historyAdapter = _inkHistorySqliteAdapter;
        if (historyAdapter == null)
        {
            persistence.SaveInkForFile(sourcePath, pageIndex, strokes);
            return;
        }

        var strokesJson = SerializeInkStrokes(strokes);
        historyAdapter.Save(sourcePath, pageIndex, strokesJson);
    }

    private List<InkStrokeData> LoadInkHistorySnapshot(
        string sourcePath,
        int pageIndex,
        InkPersistenceService persistence)
    {
        var historyAdapter = _inkHistorySqliteAdapter;
        if (historyAdapter == null)
        {
            return persistence.LoadInkPageForFile(sourcePath, pageIndex) ?? new List<InkStrokeData>();
        }

        var result = historyAdapter.LoadOrCreate(sourcePath, pageIndex, writeSnapshot: _inkSaveEnabled);
        return DeserializeInkStrokes(result.StrokesJson);
    }

    private static string? SerializeInkStrokes(List<InkStrokeData> strokes)
    {
        if (strokes == null || strokes.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(strokes, InkHistoryJsonOptions);
    }

    private static List<InkStrokeData> DeserializeInkStrokes(string? strokesJson)
    {
        if (string.IsNullOrWhiteSpace(strokesJson))
        {
            return new List<InkStrokeData>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<InkStrokeData>>(strokesJson, InkHistoryJsonOptions) ?? new List<InkStrokeData>();
        }
        catch (JsonException)
        {
            return new List<InkStrokeData>();
        }
    }

    /// <summary>
    /// Handler for the export button click on both sidebars.
    /// </summary>
    private void OnExportInkClick(object sender, RoutedEventArgs e)
    {
        if (_inkPersistence == null || _inkExport == null)
        {
            ShowExportMessageSafe("export-click-service-uninitialized", "笔迹服务未初始化。", "导出失败", MessageBoxImage.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            ShowExportMessageSafe("export-click-document-empty", "当前没有打开的文件。", "导出失败", MessageBoxImage.Warning);
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
            ShowExportMessageSafe("export-current-no-ink", "当前文件没有笔迹，无法导出。", "导出笔迹合成图片", MessageBoxImage.Information);
            return;
        }

        // Check overwrite
        var existing = _inkExport.GetExistingOutputPaths(_currentDocumentPath, inkDoc, _inkExportOptions);
        if (existing.Count > 0)
        {
            var result = ShowExportConfirmationSafe(
                "export-current-overwrite-check",
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
            ShowExportMessageSafe(
                "export-current-complete",
                $"导出完成：新增 {exportResult.ExportedCount}，跳过 {exportResult.SkippedCount}，失败 {exportResult.FailedCount}\n输出文件 {outputs.Count} 个到：\n{InkExportService.GetExportDirectory(_currentDocumentPath)}",
                "导出完成",
                MessageBoxImage.Information);
        }
        else
        {
            ShowExportMessageSafe("export-current-empty", "没有可导出的内容。", "导出", MessageBoxImage.Information);
        }
    }

    private void ExportDirectory(string directoryPath)
    {
        if (_inkExport == null || _inkPersistence == null)
        {
            return;
        }
        if (Volatile.Read(ref _overlayClosed) != 0)
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
            ShowExportMessageSafe("export-directory-empty", "当前目录没有可导出的笔迹合成记录。", "导出", MessageBoxImage.Information);
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
            var overwriteChoice = ShowExportConfirmationSafe(
                "export-directory-overwrite-check",
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
        var progressWindowShown = false;
        SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                progressWindow.Show();
                progressWindowShown = true;
            },
            ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport][progress-window-show] failed: {ex.GetType().Name} - {ex.Message}"));

        var exportOptions = _inkExportOptions;
        var exportService = _inkExport;
        var lifecycleToken = _overlayLifecycleCancellation.Token;

        _ = SafeTaskRunner.Run(
            "PaintOverlayWindow.ExportDirectory",
            token =>
            {
                token.ThrowIfCancellationRequested();
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
                    MaxDegreeOfParallelism = maxParallel,
                    CancellationToken = token
                };

                Parallel.ForEach(exportQueue, parallelOptions, sourcePath =>
                {
                    token.ThrowIfCancellationRequested();
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
                    DispatchExportUiUpdate("directory-progress-update", () =>
                    {
                        progressText.Text = $"正在导出 ({done}/{total}, 并发 {maxParallel}): {Path.GetFileName(sourcePath)}";
                    });
                });

                DispatchExportUiUpdate("directory-export-complete", () =>
                {
                    if (progressWindowShown)
                    {
                        SafeActionExecutionExecutor.TryExecute(
                            progressWindow.Close,
                            ex => System.Diagnostics.Debug.WriteLine(
                                $"[InkExport][progress-window-close] failed: {ex.GetType().Name} - {ex.Message}"));
                    }
                    ShowExportMessageSafe(
                        "export-directory-complete",
                        $"导出完成：新增 {exportedCount}，跳过 {skippedCount}，失败 {failedCount}\n输出文件 {outputs.Count} 个。",
                        "导出完成",
                        MessageBoxImage.Information);
                });
            },
            lifecycleToken,
            ex =>
            {
                DispatchExportUiUpdate("directory-export-failed", () =>
                {
                    if (progressWindowShown)
                    {
                        SafeActionExecutionExecutor.TryExecute(
                            progressWindow.Close,
                            closeEx => System.Diagnostics.Debug.WriteLine(
                                $"[InkExport][progress-window-close] failed: {closeEx.GetType().Name} - {closeEx.Message}"));
                    }

                    ShowExportMessageSafe(
                        "export-directory-failed",
                        $"导出过程中出现异常：{ex.Message}",
                        "导出失败",
                        MessageBoxImage.Warning);
                });
            });
    }

    private static long EstimateExportWorkload(string sourcePath, InkDocumentData? inkDoc)
    {
        var hasInkPages = inkDoc?.Pages?.Count(page => page.Strokes?.Count > 0) ?? 0;
        var pageWeight = Math.Max(1, hasInkPages);
        var sizeWeight = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
                {
                    return Math.Max(1, new FileInfo(sourcePath).Length / (512 * 1024));
                }

                return 1L;
            },
            fallback: 1L,
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport] Estimate workload fallback for '{sourcePath}': {ex.GetType().Name} - {ex.Message}"));
        return pageWeight * sizeWeight;
    }

    private InkDocumentData? BuildInkDocumentForExport(string sourceFilePath)
    {
        if (_inkPersistence == null || string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return null;
        }

        PurgePersistedInkForHiddenSourceIfNeeded(sourceFilePath);

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
            InkExportSnapshotBuilder.UpsertPageStrokes(
                inkDoc,
                sourceFilePath,
                _currentPageIndex,
                CloneCommittedInkStrokes());
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
        ShowExportMessageSafe(
            "export-preflush-failed",
            $"导出前自动保存校验失败，已中止导出。\n失败 {flushResult.Failures.Count} 页：\n{details}",
            "导出已中止",
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

