using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Ink;

public sealed partial class InkExportService
{
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
}
