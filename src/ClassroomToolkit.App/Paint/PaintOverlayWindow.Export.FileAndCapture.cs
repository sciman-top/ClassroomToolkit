using System;
using System.IO;
using System.Windows;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private static string BuildTimestampedPersistentCapturePath(string? extension, string? preferredDirectory)
    {
        var normalizedExtension = string.IsNullOrWhiteSpace(extension)
            ? ".png"
            : extension!;
        var captureRoot = ResolvePersistentCaptureRootDirectory(preferredDirectory);
        Directory.CreateDirectory(captureRoot);

        var timestamp = DateTime.Now;
        var baseName = $"capture-{timestamp:yyyyMMdd-HHmmss-fff}";
        var candidatePath = Path.Combine(captureRoot, $"{baseName}{normalizedExtension}");
        var suffix = 1;
        while (File.Exists(candidatePath))
        {
            candidatePath = Path.Combine(captureRoot, $"{baseName}-{suffix:D2}{normalizedExtension}");
            suffix++;
        }

        return candidatePath;
    }

    private static string ResolvePersistentCaptureRootDirectory(string? preferredDirectory)
    {
        if (!string.IsNullOrWhiteSpace(preferredDirectory))
        {
            try
            {
                var fullPath = Path.GetFullPath(preferredDirectory);
                if (Directory.Exists(fullPath))
                {
                    return fullPath;
                }
            }
            catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                System.Diagnostics.Debug.WriteLine($"[InkExport] preferred capture export root invalid: {ex.Message}");
            }
        }

        return RegionScreenCaptureWorkflow.GetPersistentCaptureRootDirectory();
    }

    private bool ExecuteSessionCaptureExportCore()
    {
        var exportService = _inkExport;
        var sourcePath = _currentDocumentPath;
        if (exportService == null || string.IsNullOrWhiteSpace(sourcePath))
        {
            return false;
        }

        return SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                var savedSourcePath = BuildTimestampedPersistentCapturePath(
                    Path.GetExtension(sourcePath),
                    _sessionCaptureExportDirectory);
                File.Copy(sourcePath, savedSourcePath, overwrite: false);

                var inkDoc = BuildInkDocumentForExport(sourcePath);
                var exportResult = exportService.ExportAllPagesForFileDetailed(savedSourcePath, inkDoc, _inkExportOptions);
                var compositeDir = InkExportService.GetExportDirectory(savedSourcePath);
                ShowExportMessageSafe(
                    "export-session-capture-complete",
                    $"导出完成：\n原图：{savedSourcePath}\n合成图目录：{compositeDir}",
                    "导出完成",
                    MessageBoxImage.Information);

                System.Diagnostics.Debug.WriteLine(
                    $"[InkExport] session capture exported source={savedSourcePath}, compositeCount={exportResult.OutputPaths.Count}, compositeDir={compositeDir}");
                return true;
            },
            fallback: false,
            ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport] export session capture failed: {ex.GetType().Name} - {ex.Message}"));
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
}
