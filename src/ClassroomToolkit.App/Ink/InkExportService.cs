using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Method B: Export composite images (original + ink overlay) to disk.
/// PDF pages are rendered to bitmap first, then ink strokes are drawn on top.
/// </summary>
public sealed class InkExportService
{
    private const string ExportFolderName = "笔迹合成图片";
    private const string ExportManifestFileName = ".ink-export.manifest.json";
    private static readonly ConcurrentDictionary<string, object> ManifestWriteLocks = new(StringComparer.OrdinalIgnoreCase);

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
    public List<string> ExportAllPagesForFile(
        string sourcePath,
        InkDocumentData? inkDoc,
        InkExportOptions options)
    {
        return ExportAllPagesForFileDetailed(sourcePath, inkDoc, options).OutputPaths;
    }

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
    /// List source files in the given directory that already have Method B composite outputs.
    /// </summary>
    public IReadOnlyList<string> ListFilesWithCompositeExports(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Array.Empty<string>();
        }

        var exportDir = Path.Combine(directoryPath, ExportFolderName);
        if (!Directory.Exists(exportDir))
        {
            return Array.Empty<string>();
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var outputPath in GetFilesSafely(exportDir))
        {
            var fileName = Path.GetFileName(outputPath);
            if (!TryResolveSourcePathFromCompositeName(directoryPath, fileName, out var sourcePath))
            {
                continue;
            }

            if (File.Exists(sourcePath))
            {
                result.Add(sourcePath);
            }
        }

        return result.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>
    /// Remove orphan composite outputs whose source files no longer exist.
    /// Returns deleted output count.
    /// </summary>
    public int CleanupOrphanCompositeOutputsInDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return 0;
        }

        var exportDir = Path.Combine(directoryPath, ExportFolderName);
        if (!Directory.Exists(exportDir))
        {
            return 0;
        }

        var manifest = LoadExportManifest(exportDir);
        var manifestDirty = false;
        var deleted = 0;
        foreach (var outputPath in GetFilesSafely(exportDir))
        {
            var fileName = Path.GetFileName(outputPath);
            if (!TryResolveSourcePathFromCompositeName(directoryPath, fileName, out var sourcePath))
            {
                continue;
            }

            if (File.Exists(sourcePath))
            {
                continue;
            }

            if (!TryDeleteOutputFileSafe(outputPath))
            {
                continue;
            }
            deleted++;
            manifestDirty = true;

            if (manifest.Remove(GetManifestKey(outputPath)))
            {
                manifestDirty = true;
            }
        }

        if (manifestDirty)
        {
            SaveExportManifest(exportDir, manifest);
        }
        return deleted;
    }

    /// <summary>
    /// Get the export directory path for the given source file.
    /// </summary>
    public static string GetExportDirectory(string sourcePath)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        return Path.Combine(sourceDir, ExportFolderName);
    }

    /// <summary>
    /// Check whether the export output files already exist for a given source file.
    /// Returns a list of paths that would be overwritten.
    /// </summary>
    public List<string> GetExistingOutputPaths(string sourcePath, InkDocumentData? inkDoc, InkExportOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var existing = new List<string>();
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return existing;
        }

        if (IsPdf(sourcePath))
        {
            var pagesToCheck = (inkDoc?.Pages ?? new List<InkPageData>())
                .Where(p => p.PageIndex >= 1 && p.Strokes != null && p.Strokes.Count > 0)
                .Select(p => p.PageIndex)
                .Distinct()
                .OrderBy(i => i)
                .ToList();

            if (pagesToCheck.Count == 0)
            {
                return existing;
            }

            foreach (var pageIndex in pagesToCheck)
            {
                var path = BuildOutputPath(sourcePath, pageIndex, isPdf: true, options);
                if (File.Exists(path))
                {
                    existing.Add(path);
                }
            }
        }
        else
        {
            var path = BuildOutputPath(sourcePath, 1, isPdf: false, options);
            if (File.Exists(path))
            {
                existing.Add(path);
            }
        }
        return existing;
    }

    /// <summary>
    /// Remove composite export outputs for a specific source page.
    /// For images, pageIndex is treated as page 1.
    /// Returns number of deleted files.
    /// </summary>
    public int RemoveCompositeOutputsForPage(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return 0;
        }

        var isPdf = IsPdf(sourcePath);
        if (!isPdf)
        {
            pageIndex = 1;
        }
        else if (pageIndex <= 0)
        {
            return 0;
        }

        var exportDir = GetExportDirectory(sourcePath);
        if (!Directory.Exists(exportDir))
        {
            return 0;
        }

        var manifest = LoadExportManifest(exportDir);
        var manifestDirty = false;
        var deleted = 0;
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        foreach (var outputPath in GetFilesSafely(exportDir))
        {
            var fileName = Path.GetFileName(outputPath);
            if (!TryResolveSourcePathFromCompositeName(sourceDir, fileName, out var resolvedSourcePath))
            {
                continue;
            }

            if (!PathsEqual(sourcePath, resolvedSourcePath))
            {
                continue;
            }

            if (isPdf)
            {
                if (!TryGetPdfPageIndexFromCompositeName(fileName, out var outputPageIndex) || outputPageIndex != pageIndex)
                {
                    continue;
                }
            }

            if (!TryDeleteOutputFileSafe(outputPath))
            {
                continue;
            }
            deleted++;
            manifestDirty = true;

            if (manifest.Remove(GetManifestKey(outputPath)))
            {
                manifestDirty = true;
            }
        }

        if (manifestDirty)
        {
            SaveExportManifest(exportDir, manifest);
        }
        return deleted;
    }

    // ── Private helpers ──

    private void ExportPdfFile(string sourcePath, InkDocumentData? inkDoc, InkExportOptions options, InkExportRunResult result)
    {
        var exportDir = GetExportDirectory(sourcePath);
        var manifest = LoadExportManifest(exportDir);
        var manifestDirty = false;

        var pagesWithInk = (inkDoc?.Pages ?? new List<InkPageData>())
            .Where(p => p.PageIndex >= 1 && p.Strokes != null && p.Strokes.Count > 0)
            .GroupBy(p => p.PageIndex)
            .Select(g => g.OrderByDescending(x => x.UpdatedAt).First())
            .OrderBy(p => p.PageIndex)
            .ToList();

        var expectedOutputFileNames = new HashSet<string>(
            pagesWithInk
                .Select(p => Path.GetFileName(BuildOutputPath(sourcePath, p.PageIndex, isPdf: true, options))),
            StringComparer.OrdinalIgnoreCase);
        manifestDirty |= CleanupStaleCompositeOutputsForSource(sourcePath, exportDir, expectedOutputFileNames, manifest);
        if (pagesWithInk.Count == 0)
        {
            if (manifestDirty)
            {
                SaveExportManifest(exportDir, manifest);
            }
            return;
        }

        PdfDocumentHost? pdfDoc = null;
        try
        {
            pdfDoc = PdfDocumentHost.Open(sourcePath);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            if (manifestDirty)
            {
                SaveExportManifest(exportDir, manifest);
            }
            return;
        }

        using (pdfDoc)
        {
        int pageCount = pdfDoc.PageCount;
        if (pageCount <= 0)
        {
            if (manifestDirty)
            {
                SaveExportManifest(exportDir, manifest);
            }
            return;
        }

        pagesWithInk = pagesWithInk
            .Where(p => p.PageIndex <= pageCount)
            .ToList();
        expectedOutputFileNames = new HashSet<string>(
            pagesWithInk
                .Select(p => Path.GetFileName(BuildOutputPath(sourcePath, p.PageIndex, isPdf: true, options))),
            StringComparer.OrdinalIgnoreCase);
        manifestDirty |= CleanupStaleCompositeOutputsForSource(sourcePath, exportDir, expectedOutputFileNames, manifest);
        if (pagesWithInk.Count == 0)
        {
            if (manifestDirty)
            {
                SaveExportManifest(exportDir, manifest);
            }
            return;
        }

        Directory.CreateDirectory(exportDir);

        var dpiScale = Math.Max(0.0001, options.Dpi / 96.0);
        var renderedPageCache = new Dictionary<int, BitmapSource>();
        foreach (var page in pagesWithInk)
        {
            var pageIndex = page.PageIndex;
            var outputPath = BuildOutputPath(sourcePath, pageIndex, isPdf: true, options);
            var pageFingerprint = BuildExportFingerprint(sourcePath, pageIndex, page.Strokes, options);
            if (ShouldSkipExport(outputPath, pageFingerprint, manifest))
            {
                result.OutputPaths.Add(outputPath);
                result.SkippedCount++;
                continue;
            }

            var background = GetOrRenderPdfPage(renderedPageCache, pdfDoc, pageIndex, options.Dpi);
            if (background == null)
            {
                result.FailedCount++;
                continue;
            }

            var strokes = AdaptStrokesForBackground(page.Strokes, background, fallbackScale: dpiScale);
            var outputBitmap = CompositeImage(background, strokes);

            SaveImage(outputBitmap, outputPath, options);
            manifest[GetManifestKey(outputPath)] = pageFingerprint;
            manifestDirty = true;
            result.OutputPaths.Add(outputPath);
            result.ExportedCount++;
        }

        if (manifestDirty)
        {
            SaveExportManifest(exportDir, manifest);
        }
        }
    }

    private void ExportImageFile(string sourcePath, InkDocumentData? inkDoc, InkExportOptions options, InkExportRunResult result)
    {
        var background = LoadImage(sourcePath);
        if (background == null)
        {
            result.FailedCount++;
            return;
        }

        var rawStrokes = inkDoc?.Pages.FirstOrDefault(p => p.PageIndex == 1)?.Strokes
                      ?? inkDoc?.Pages.FirstOrDefault()?.Strokes;
        var strokes = AdaptStrokesForBackground(rawStrokes, background, fallbackScale: 1.0);

        BitmapSource outputBitmap;
        if (strokes.Count > 0)
        {
            outputBitmap = CompositeImage(background, strokes);
        }
        else
        {
            // Even if no ink, still export (user request: export all pages including without ink)
            outputBitmap = background;
        }

        var exportDir = GetExportDirectory(sourcePath);
        Directory.CreateDirectory(exportDir);
        var manifest = LoadExportManifest(exportDir);
        var outputPath = BuildOutputPath(sourcePath, 1, isPdf: false, options);
        var fingerprintSourceStrokes = rawStrokes ?? new List<InkStrokeData>();
        var pageFingerprint = BuildExportFingerprint(sourcePath, 1, fingerprintSourceStrokes, options);
        if (ShouldSkipExport(outputPath, pageFingerprint, manifest))
        {
            result.OutputPaths.Add(outputPath);
            result.SkippedCount++;
            return;
        }

        SaveImage(outputBitmap, outputPath, options);
        manifest[GetManifestKey(outputPath)] = pageFingerprint;
        SaveExportManifest(exportDir, manifest);
        result.OutputPaths.Add(outputPath);
        result.ExportedCount++;
    }

    private BitmapSource CompositeImage(BitmapSource background, List<InkStrokeData> strokes)
    {
        int pixelWidth = background.PixelWidth;
        int pixelHeight = background.PixelHeight;
        double dpiX = background.DpiX;
        double dpiY = background.DpiY;
        if (dpiX <= 0)
        {
            dpiX = 96.0;
        }
        if (dpiY <= 0)
        {
            dpiY = 96.0;
        }
        var widthDip = pixelWidth * 96.0 / dpiX;
        var heightDip = pixelHeight * 96.0 / dpiY;

        var page = new InkPageData { Strokes = strokes };
        var renderer = new InkStrokeRenderer();
        var inkLayer = renderer.RenderPage(page, pixelWidth, pixelHeight, dpiX, dpiY);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(background, new Rect(0, 0, widthDip, heightDip));
            dc.DrawImage(inkLayer, new Rect(0, 0, widthDip, heightDip));
        }

        var result = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    private static BitmapSource? LoadBackground(string sourcePath, int pageIndex, int dpi)
    {
        if (IsPdf(sourcePath))
        {
            return LoadPdfPage(sourcePath, pageIndex, dpi);
        }
        return LoadImage(sourcePath);
    }

    private static BitmapSource? LoadPdfPage(string sourcePath, int pageIndex, int dpi)
    {
        try
        {
            using var doc = PdfDocumentHost.Open(sourcePath);
            return doc.RenderPage(pageIndex, dpi);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return null;
        }
    }

    private static BitmapSource? LoadImage(string sourcePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return null;
        }
    }

    private static int GetPdfPageCount(string sourcePath)
    {
        try
        {
            using var doc = PdfDocumentHost.Open(sourcePath);
            return doc.PageCount;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return 0;
        }
    }

    private static string BuildOutputPath(string sourcePath, int pageIndex, bool isPdf, InkExportOptions options)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var exportDir = Path.Combine(sourceDir, ExportFolderName);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        string extension;
        if (isPdf)
        {
            // PDF always exports as PNG (or configured format)
            extension = options.Format.Equals("JPG", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
        }
        else
        {
            // Image: keep original format
            extension = Path.GetExtension(sourcePath)?.ToLowerInvariant() ?? ".png";
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }
        }

        string fileName;
        if (isPdf)
        {
            var pageStr = pageIndex.ToString("D3", CultureInfo.InvariantCulture);
            fileName = $"{baseName}_P{pageStr}+笔迹{extension}";
        }
        else
        {
            fileName = $"{baseName}+笔迹{extension}";
        }

        return Path.Combine(exportDir, fileName);
    }

    private static bool TryResolveSourcePathFromCompositeName(string sourceDirectory, string outputFileName, out string sourcePath)
    {
        sourcePath = string.Empty;
        if (string.IsNullOrWhiteSpace(sourceDirectory) || string.IsNullOrWhiteSpace(outputFileName))
        {
            return false;
        }

        var extension = Path.GetExtension(outputFileName);
        var withoutExtension = Path.GetFileNameWithoutExtension(outputFileName);
        const string suffix = "+笔迹";
        if (string.IsNullOrWhiteSpace(extension) || string.IsNullOrWhiteSpace(withoutExtension) ||
            !withoutExtension.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var namePrefix = withoutExtension[..^suffix.Length];
        if (string.IsNullOrWhiteSpace(namePrefix))
        {
            return false;
        }

        var pageMarker = namePrefix.LastIndexOf("_P", StringComparison.Ordinal);
        if (pageMarker >= 0 && namePrefix.Length - pageMarker == 5)
        {
            var pageNumberPart = namePrefix[(pageMarker + 2)..];
            if (pageNumberPart.All(char.IsDigit))
            {
                var pdfBaseName = namePrefix[..pageMarker];
                if (string.IsNullOrWhiteSpace(pdfBaseName))
                {
                    return false;
                }

                sourcePath = Path.Combine(sourceDirectory, $"{pdfBaseName}.pdf");
                return true;
            }
        }

        sourcePath = Path.Combine(sourceDirectory, $"{namePrefix}{extension}");
        return true;
    }

    private static bool TryGetPdfPageIndexFromCompositeName(string outputFileName, out int pageIndex)
    {
        pageIndex = 0;
        if (string.IsNullOrWhiteSpace(outputFileName))
        {
            return false;
        }

        var withoutExtension = Path.GetFileNameWithoutExtension(outputFileName);
        const string suffix = "+笔迹";
        if (string.IsNullOrWhiteSpace(withoutExtension) || !withoutExtension.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        var namePrefix = withoutExtension[..^suffix.Length];
        var pageMarker = namePrefix.LastIndexOf("_P", StringComparison.Ordinal);
        if (pageMarker < 0 || namePrefix.Length - pageMarker != 5)
        {
            return false;
        }

        var pageRaw = namePrefix[(pageMarker + 2)..];
        return int.TryParse(pageRaw, out pageIndex) && pageIndex > 0;
    }

    private static void SaveImage(BitmapSource bitmap, string outputPath, InkExportOptions options)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        BitmapEncoder encoder;
        var ext = Path.GetExtension(outputPath)?.ToLowerInvariant();
        if (ext == ".jpg" || ext == ".jpeg")
        {
            encoder = new JpegBitmapEncoder
            {
                QualityLevel = Math.Clamp(options.JpegQuality, 1, 100)
            };
        }
        else
        {
            encoder = new PngBitmapEncoder();
        }

        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }

    private static bool IsPdf(string path)
    {
        var ext = Path.GetExtension(path);
        return !string.IsNullOrWhiteSpace(ext) && ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldSkipExport(string outputPath, string fingerprint, Dictionary<string, string> manifest)
    {
        if (!File.Exists(outputPath))
        {
            return false;
        }

        return manifest.TryGetValue(GetManifestKey(outputPath), out var recorded)
               && string.Equals(recorded, fingerprint, StringComparison.Ordinal);
    }

    private static bool TryDeleteOutputFileSafe(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return false;
        }
    }

    private static void CleanupCompositeOutputsForFilesWithoutInk(string directoryPath, HashSet<string> filesWithInkSet)
    {
        var exportDir = Path.Combine(directoryPath, ExportFolderName);
        if (!Directory.Exists(exportDir))
        {
            return;
        }

        var filesWithComposite = ListCompositeSourceFilesInDirectory(directoryPath);
        if (filesWithComposite.Count == 0)
        {
            return;
        }

        var manifest = LoadExportManifest(exportDir);
        var manifestDirty = false;
        foreach (var sourcePath in filesWithComposite)
        {
            if (filesWithInkSet.Contains(NormalizePathOrOriginal(sourcePath)))
            {
                continue;
            }

            manifestDirty |= CleanupStaleCompositeOutputsForSource(
                sourcePath,
                exportDir,
                keepOutputFileNames: new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                manifest);
        }

        if (manifestDirty)
        {
            SaveExportManifest(exportDir, manifest);
        }
    }

    private static IReadOnlyList<string> ListCompositeSourceFilesInDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return Array.Empty<string>();
        }

        var exportDir = Path.Combine(directoryPath, ExportFolderName);
        if (!Directory.Exists(exportDir))
        {
            return Array.Empty<string>();
        }

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var outputPath in GetFilesSafely(exportDir))
        {
            var fileName = Path.GetFileName(outputPath);
            if (!TryResolveSourcePathFromCompositeName(directoryPath, fileName, out var sourcePath))
            {
                continue;
            }

            if (File.Exists(sourcePath))
            {
                result.Add(sourcePath);
            }
        }

        return result.OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static bool CleanupStaleCompositeOutputsForSource(
        string sourcePath,
        string exportDir,
        HashSet<string> keepOutputFileNames,
        Dictionary<string, string> manifest)
    {
        if (!Directory.Exists(exportDir))
        {
            return false;
        }

        var dirty = false;
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        foreach (var outputPath in GetFilesSafely(exportDir))
        {
            var fileName = Path.GetFileName(outputPath);
            if (!TryResolveSourcePathFromCompositeName(sourceDir, fileName, out var resolvedSourcePath))
            {
                continue;
            }

            if (!PathsEqual(sourcePath, resolvedSourcePath))
            {
                continue;
            }

            if (keepOutputFileNames.Contains(fileName))
            {
                continue;
            }

            if (!TryDeleteOutputFileSafe(outputPath))
            {
                continue;
            }
            dirty = true;

            if (manifest.Remove(GetManifestKey(outputPath)))
            {
                dirty = true;
            }
        }

        return dirty;
    }

    private static bool PathsEqual(string left, string right)
    {
        var leftFull = GetFullPathOrOriginal(left);
        var rightFull = GetFullPathOrOriginal(right);
        return string.Equals(leftFull, rightFull, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePathOrOriginal(string path)
    {
        return GetFullPathOrOriginal(path);
    }

    private static string GetFullPathOrOriginal(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return path;
        }
    }

    private static IReadOnlyList<string> GetFilesSafely(string directoryPath)
    {
        try
        {
            return Directory.GetFiles(directoryPath);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return Array.Empty<string>();
        }
    }

    private static string BuildExportFingerprint(
        string sourcePath,
        int pageIndex,
        IReadOnlyList<InkStrokeData> strokes,
        InkExportOptions options)
    {
        var sourceTicks = 0L;
        try
        {
            sourceTicks = File.Exists(sourcePath) ? File.GetLastWriteTimeUtc(sourcePath).Ticks : 0L;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            sourceTicks = 0L;
        }

        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        InkExportFingerprintUtilities.AppendHashField(hash, sourcePath);
        InkExportFingerprintUtilities.AppendHashField(hash, sourceTicks.ToString(CultureInfo.InvariantCulture));
        InkExportFingerprintUtilities.AppendHashField(hash, pageIndex.ToString(CultureInfo.InvariantCulture));
        InkExportFingerprintUtilities.AppendHashField(hash, options.Dpi.ToString(CultureInfo.InvariantCulture));
        InkExportFingerprintUtilities.AppendHashField(hash, options.Format);
        InkExportFingerprintUtilities.AppendHashField(hash, options.JpegQuality.ToString(CultureInfo.InvariantCulture));

        foreach (var stroke in strokes)
        {
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.Type.ToString());
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.BrushStyle.ToString());
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.ColorHex);
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.Opacity.ToString(CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.BrushSize.ToString("G17", CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.ReferenceWidth.ToString("G17", CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.ReferenceHeight.ToString("G17", CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.CalligraphyRenderMode.ToString());
            InkExportFingerprintUtilities.AppendHashToken(hash, (stroke.CalligraphyInkBloomEnabled ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, (stroke.CalligraphySealEnabled ? 1 : 0).ToString(CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.CalligraphyOverlayOpacityThreshold.ToString(CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.MaskSeed.ToString(CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.InkFlow.ToString("G17", CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.StrokeDirectionX.ToString("G17", CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashToken(hash, stroke.StrokeDirectionY.ToString("G17", CultureInfo.InvariantCulture));
            InkExportFingerprintUtilities.AppendHashField(hash, stroke.GeometryPath);

            foreach (var ribbon in stroke.Ribbons)
            {
                InkExportFingerprintUtilities.AppendHashUtf8(hash, "r:");
                InkExportFingerprintUtilities.AppendHashToken(hash, ribbon.RibbonT.ToString("G17", CultureInfo.InvariantCulture));
                InkExportFingerprintUtilities.AppendHashToken(hash, ribbon.Opacity.ToString("G17", CultureInfo.InvariantCulture));
                InkExportFingerprintUtilities.AppendHashField(hash, ribbon.GeometryPath);
            }

            foreach (var bloom in stroke.Blooms)
            {
                InkExportFingerprintUtilities.AppendHashUtf8(hash, "b:");
                InkExportFingerprintUtilities.AppendHashToken(hash, bloom.Opacity.ToString("G17", CultureInfo.InvariantCulture));
                InkExportFingerprintUtilities.AppendHashField(hash, bloom.GeometryPath);
            }

            InkExportFingerprintUtilities.AppendHashUtf8(hash, ";");
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    private static string GetManifestPath(string exportDir)
    {
        return Path.Combine(exportDir, ExportManifestFileName);
    }

    private static string GetManifestKey(string outputPath)
    {
        return Path.GetFileName(outputPath);
    }

    private static Dictionary<string, string> LoadExportManifest(string exportDir)
    {
        var path = GetManifestPath(exportDir);
        if (!File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var raw = File.ReadAllText(path);
            var map = JsonSerializer.Deserialize<Dictionary<string, string>>(raw);
            return map != null
                ? new Dictionary<string, string>(map, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static BitmapSource? GetOrRenderPdfPage(
        Dictionary<int, BitmapSource> cache,
        PdfDocumentHost pdfDoc,
        int pageIndex,
        int dpi)
    {
        if (cache.TryGetValue(pageIndex, out var cached))
        {
            return cached;
        }

        try
        {
            var rendered = pdfDoc.RenderPage(pageIndex, dpi);
            if (rendered != null)
            {
                cache[pageIndex] = rendered;
            }
            return rendered;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return null;
        }
    }

    private static void SaveExportManifest(string exportDir, Dictionary<string, string> manifest)
    {
        try
        {
            Directory.CreateDirectory(exportDir);
            var path = GetManifestPath(exportDir);
            var writeLock = ManifestWriteLocks.GetOrAdd(path, _ => new object());
            lock (writeLock)
            {
                var merged = File.Exists(path)
                    ? LoadExportManifest(exportDir)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                foreach (var pair in manifest)
                {
                    merged[pair.Key] = pair.Value;
                }

                var staleKeys = merged.Keys
                    .Where(key => string.IsNullOrWhiteSpace(key) || !File.Exists(Path.Combine(exportDir, key)))
                    .ToList();
                foreach (var key in staleKeys)
                {
                    merged.Remove(key);
                }

                var json = JsonSerializer.Serialize(merged, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(path, json);
            }
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            // Ignore manifest write failures; export output is still valid.
        }
    }

    private static List<InkStrokeData> AdaptStrokesForBackground(IReadOnlyList<InkStrokeData>? strokes, BitmapSource background, double fallbackScale)
    {
        if (strokes == null || strokes.Count == 0)
        {
            return new List<InkStrokeData>();
        }

        var targetWidth = InkExportScaleUtilities.GetBitmapWidthDip(background);
        var targetHeight = InkExportScaleUtilities.GetBitmapHeightDip(background);
        if (targetWidth <= 0.5 || targetHeight <= 0.5)
        {
            return strokes.Select(CloneStroke).ToList();
        }

        var result = new List<InkStrokeData>(strokes.Count);
        foreach (var stroke in strokes)
        {
            var scaleX = InkExportScaleUtilities.ResolveScale(targetWidth, stroke.ReferenceWidth, fallbackScale);
            var scaleY = InkExportScaleUtilities.ResolveScale(targetHeight, stroke.ReferenceHeight, fallbackScale);
            var scaled = CloneStroke(stroke);
            scaled.GeometryPath = ScaleGeometryPath(stroke.GeometryPath, scaleX, scaleY);
            var brushScale = Math.Sqrt(Math.Abs(scaleX * scaleY));
            scaled.BrushSize = Math.Max(0.1, scaled.BrushSize * brushScale);
            scaled.ReferenceWidth = targetWidth;
            scaled.ReferenceHeight = targetHeight;
            if (scaled.Ribbons.Count > 0)
            {
                for (int i = 0; i < scaled.Ribbons.Count; i++)
                {
                    var ribbon = scaled.Ribbons[i];
                    ribbon.GeometryPath = ScaleGeometryPath(ribbon.GeometryPath, scaleX, scaleY);
                }
            }
            if (scaled.Blooms.Count > 0)
            {
                for (int i = 0; i < scaled.Blooms.Count; i++)
                {
                    var bloom = scaled.Blooms[i];
                    bloom.GeometryPath = ScaleGeometryPath(bloom.GeometryPath, scaleX, scaleY);
                }
            }
            result.Add(scaled);
        }
        return result;
    }

    private static string ScaleGeometryPath(string geometryPath, double scaleX, double scaleY)
    {
        if (string.IsNullOrWhiteSpace(geometryPath))
        {
            return string.Empty;
        }

        var geometry = InkGeometrySerializer.Deserialize(geometryPath);
        if (geometry == null)
        {
            return geometryPath;
        }

        var clone = geometry.Clone();
        clone.Transform = new ScaleTransform(scaleX, scaleY);
        var flattened = clone.GetFlattenedPathGeometry();
        return InkGeometrySerializer.Serialize(flattened);
    }

    private static InkStrokeData CloneStroke(InkStrokeData stroke)
    {
        return new InkStrokeData
        {
            Type = stroke.Type,
            BrushStyle = stroke.BrushStyle,
            GeometryPath = stroke.GeometryPath,
            ColorHex = stroke.ColorHex,
            Opacity = stroke.Opacity,
            BrushSize = stroke.BrushSize,
            MaskSeed = stroke.MaskSeed,
            InkFlow = stroke.InkFlow,
            StrokeDirectionX = stroke.StrokeDirectionX,
            StrokeDirectionY = stroke.StrokeDirectionY,
            CalligraphyRenderMode = stroke.CalligraphyRenderMode,
            ReferenceWidth = stroke.ReferenceWidth,
            ReferenceHeight = stroke.ReferenceHeight,
            Ribbons = stroke.Ribbons
                .Select(r => new InkRibbonData
                {
                    GeometryPath = r.GeometryPath,
                    Opacity = r.Opacity,
                    RibbonT = r.RibbonT
                })
                .ToList(),
            CalligraphyInkBloomEnabled = stroke.CalligraphyInkBloomEnabled,
            CalligraphySealEnabled = stroke.CalligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = stroke.CalligraphyOverlayOpacityThreshold,
            Blooms = stroke.Blooms
                .Select(b => new InkBloomData
                {
                    GeometryPath = b.GeometryPath,
                    Opacity = b.Opacity
                })
                .ToList()
        };
    }
}
