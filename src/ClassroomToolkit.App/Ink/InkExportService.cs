using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Method B: Export composite images (original + ink overlay) to disk.
/// PDF pages are rendered to bitmap first, then ink strokes are drawn on top.
/// </summary>
public sealed partial class InkExportService
{
    private const string ExportFolderName = "笔迹合成图片";

    private readonly InkPersistenceService _persistenceService;
    public InkExportService(InkPersistenceService persistenceService)
    {
        _persistenceService = persistenceService ?? throw new ArgumentNullException(nameof(persistenceService));
    }

    public sealed class InkExportRunResult
    {
        public List<string> OutputPaths { get; } = new();
        public int ExportedCount { get; set; }
        public int SkippedCount { get; set; }
        public int FailedCount { get; set; }
    }

    /// <summary>
    /// Export a single page as a composite image (background + ink).
    /// Returns the output file path, or null if export failed.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance API for compatibility with existing callers.")]
    public string? ExportSinglePage(
        string sourcePath,
        int pageIndex,
        List<InkStrokeData> strokes,
        InkExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(sourcePath) || strokes == null || strokes.Count == 0)
        {
            return null;
        }

        try
        {
            var background = LoadBackground(sourcePath, pageIndex, options.Dpi);
            if (background == null)
            {
                return null;
            }

            var composite = CompositeImage(background, strokes);
            var outputPath = BuildOutputPath(sourcePath, pageIndex, IsPdf(sourcePath), options);
            SaveImage(composite, outputPath, options);
            return outputPath;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"[InkExport] Failed to export page {pageIndex} of {sourcePath}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Export all pages for a single file. For images, exports 1 page.
    /// For PDF, exports only pages that contain ink.
    /// Returns a list of output file paths.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance API for compatibility with existing callers.")]
    public List<string> ExportAllPagesForFile(
        string sourcePath,
        InkDocumentData? inkDoc,
        InkExportOptions options)
    {
        return ExportAllPagesForFileDetailed(sourcePath, inkDoc, options).OutputPaths;
    }

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance API for compatibility with existing callers.")]
    public InkExportRunResult ExportAllPagesForFileDetailed(
        string sourcePath,
        InkDocumentData? inkDoc,
        InkExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var result = new InkExportRunResult();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return result;
        }

        try
        {
            if (IsPdf(sourcePath))
            {
                ExportPdfFile(sourcePath, inkDoc, options, result);
            }
            else
            {
                ExportImageFile(sourcePath, inkDoc, options, result);
            }
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            result.FailedCount++;
            System.Diagnostics.Debug.WriteLine($"[InkExport] Failed to export file {sourcePath}: {ex.Message}");
        }
        return result;
    }

    /// <summary>
    /// Batch export all files with ink in the given directory.
    /// Each file's pages are exported to {directory}/笔迹合成图片/.
    /// </summary>
    public List<string> ExportAllInDirectory(
        string directoryPath,
        InkExportOptions options,
        IProgress<(int current, int total, string fileName)>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        var allOutputs = new List<string>();
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return allOutputs;
        }

        var filesWithInk = _persistenceService.ListFilesWithInk(directoryPath);
        var filesWithInkSet = new HashSet<string>(
            filesWithInk.Select(NormalizePathOrOriginal),
            StringComparer.OrdinalIgnoreCase);
        CleanupCompositeOutputsForFilesWithoutInk(directoryPath, filesWithInkSet);

        if (filesWithInk.Count == 0)
        {
            return allOutputs;
        }

        for (int i = 0; i < filesWithInk.Count; i++)
        {
            var filePath = filesWithInk[i];
            var fileName = Path.GetFileName(filePath);
            progress?.Report((i + 1, filesWithInk.Count, fileName));

            var inkDoc = _persistenceService.LoadInkForFile(filePath);
            var outputs = ExportAllPagesForFile(filePath, inkDoc, options);
            allOutputs.AddRange(outputs);
        }

        return allOutputs;
    }

    /// <summary>
    /// Get the export directory path for the given source file.
    /// </summary>
    public static string GetExportDirectory(string sourcePath)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        return Path.Combine(sourceDir, ExportFolderName);
    }
}



