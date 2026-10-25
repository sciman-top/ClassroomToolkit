using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// Ink export and persistence integration for PaintOverlayWindow.
/// Handles Method A (sidecar save/load) and Method B (composite image export).
/// </summary>
public partial class PaintOverlayWindow
{
    private static readonly JsonSerializerOptions InkHistoryJsonOptions = CreateInkHistoryJsonOptions();
    private InkPersistenceService? _inkPersistence;
    private IInkHistorySnapshotStore? _inkHistorySnapshotStore;
    private InkExportService? _inkExport;
    private InkExportOptions _inkExportOptions = new();
    private string _sessionCaptureExportDirectory = string.Empty;
    private sealed record SidecarPersistSnapshot(
        InkPersistenceService Persistence,
        string SourcePath,
        int PageIndex,
        List<InkStrokeData> Strokes,
        string SnapshotHash);

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
            "检测到该文件有笔迹，是否加载？",
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

        if (TryExportCurrentSessionRegionCapture())
        {
            return;
        }

        var sourceDir = Path.GetDirectoryName(_currentDocumentPath) ?? string.Empty;
        ExportDirectory(sourceDir);
    }

    private bool TryExportCurrentSessionRegionCapture()
    {
        if (_inkExport == null || _inkPersistence == null || string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return false;
        }

        if (!RegionScreenCaptureWorkflow.IsSessionRegionCaptureFilePath(_currentDocumentPath))
        {
            return false;
        }

        if (!File.Exists(_currentDocumentPath))
        {
            ShowExportMessageSafe("export-session-capture-missing", "截图临时文件不存在，无法导出。", "导出失败", MessageBoxImage.Warning);
            return true;
        }

        if (!FlushDirtyInkPagesToSidecarForExport(directoryPath: null))
        {
            return true;
        }

        var exported = ExecuteSessionCaptureExportCore();

        if (!exported)
        {
            ShowExportMessageSafe("export-session-capture-error", "区域截图导出失败。", "导出失败", MessageBoxImage.Warning);
        }

        return true;
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

        var filesToExport = CollectDirectoryExportCandidates(directoryPath);
        if (filesToExport.Count == 0)
        {
            ShowExportMessageSafe("export-directory-empty", "当前目录没有可导出内容。", "导出", MessageBoxImage.Information);
            return;
        }

        var exportPlan = BuildDirectoryExportPlan(filesToExport);
        var exportQueue = exportPlan.ExportQueue;
        var exportSnapshotDocs = exportPlan.ExportSnapshotDocs;
        var overwriteCount = exportPlan.OverwriteCount;

        if (!ConfirmDirectoryOverwriteIfNeeded(overwriteCount))
        {
            return;
        }

        // Run export on background thread with progress
        var (progressWindow, progressText) = CreateDirectoryExportProgressWindow();
        var progressWindowShown = false;
        void CloseProgressWindowSafe()
        {
            if (!progressWindowShown)
            {
                return;
            }

            SafeActionExecutionExecutor.TryExecute(
                progressWindow.Close,
                ex => System.Diagnostics.Debug.WriteLine(
                    $"[InkExport][progress-window-close] failed: {ex.GetType().Name} - {ex.Message}"));
        }

        SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                progressWindow.Show();
                progressWindowShown = true;
            },
            ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport][progress-window-show] failed: {ex.GetType().Name} - {ex.Message}"));

        RunDirectoryExportInBackground(
            exportQueue,
            exportSnapshotDocs,
            progressText,
            CloseProgressWindowSafe);
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
            $"自动保存失败，已中止导出。\n失败 {flushResult.Failures.Count} 页：\n{details}",
            "导出已中止",
            MessageBoxImage.Warning);
        return false;
    }

    private bool ConfirmDirectoryOverwriteIfNeeded(int overwriteCount)
    {
        if (overwriteCount <= 0)
        {
            return true;
        }

        var overwriteChoice = ShowExportConfirmationSafe(
            "export-directory-overwrite-check",
            $"导出目录中已存在 {overwriteCount} 个同名文件，是否覆盖？",
            "确认覆盖",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        return overwriteChoice == MessageBoxResult.Yes;
    }

    private static (Window ProgressWindow, System.Windows.Controls.TextBlock ProgressText) CreateDirectoryExportProgressWindow()
    {
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
        return (progressWindow, progressText);
    }

    private void RunDirectoryExportInBackground(
        List<string> exportQueue,
        Dictionary<string, InkDocumentData?> exportSnapshotDocs,
        System.Windows.Controls.TextBlock progressText,
        Action closeProgressWindowSafe)
    {
        var exportOptions = _inkExportOptions;
        var exportService = _inkExport;
        if (exportService == null)
        {
            closeProgressWindowSafe();
            ShowExportMessageSafe(
                "export-directory-service-missing",
                "导出服务不可用。",
                "导出失败",
                MessageBoxImage.Warning);
            return;
        }

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
                var maxParallel = ResolveDirectoryExportParallelism(total, exportOptions);
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
                        progressText.Text = $"导出中 ({done}/{total}, 并发 {maxParallel})：{Path.GetFileName(sourcePath)}";
                    });
                });

                DispatchExportUiUpdate("directory-export-complete", () =>
                {
                    closeProgressWindowSafe();
                    ShowExportMessageSafe(
                        "export-directory-complete",
                        $"导出完成：新增 {exportedCount}，跳过 {skippedCount}，失败 {failedCount}\n共 {outputs.Count} 个文件。",
                        "导出完成",
                        MessageBoxImage.Information);
                });
            },
            lifecycleToken,
            ex =>
            {
                DispatchExportUiUpdate("directory-export-failed", () =>
                {
                    closeProgressWindowSafe();
                    ShowExportMessageSafe(
                        "export-directory-failed",
                        $"导出异常：{ex.Message}",
                        "导出失败",
                        MessageBoxImage.Warning);
                });
            });
    }

}
