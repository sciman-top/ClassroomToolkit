using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Ink;

public sealed partial class InkExportService
{
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
        return InkExportManifestUtilities.GetManifestPath(exportDir);
    }

    private static string GetManifestKey(string outputPath)
    {
        return InkExportManifestUtilities.GetManifestKey(outputPath);
    }

    private static Dictionary<string, string> LoadExportManifest(string exportDir)
    {
        return InkExportManifestUtilities.LoadExportManifest(exportDir);
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
        InkExportManifestUtilities.SaveExportManifest(exportDir, manifest);
    }

    private static List<InkStrokeData> AdaptStrokesForBackground(List<InkStrokeData>? strokes, BitmapSource background, double fallbackScale)
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
