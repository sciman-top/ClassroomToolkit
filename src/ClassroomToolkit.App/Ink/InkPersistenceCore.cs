using ClassroomToolkit.Domain.Utilities;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Media;
using System;

namespace ClassroomToolkit.App.Ink;

/// <summary>
/// Top-level container for persisted ink annotations associated with a source file.
/// Serialized as the root object in .ink.json sidecar files.
/// </summary>
[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "List property is part of the persisted ink JSON contract.")]
[SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter is required for JSON deserialization compatibility.")]
public sealed class InkDocumentData
{
    public int Version { get; set; } = 1;
    public string SourcePath { get; set; } = string.Empty;
    public List<InkPageData> Pages { get; set; } = new();
}

internal static class InkGeometrySerializer
{
    public static string Serialize(Geometry geometry)
    {
        if (geometry == null)
        {
            return string.Empty;
        }
        var flattened = geometry.GetFlattenedPathGeometry();
        return flattened.ToString(CultureInfo.InvariantCulture);
    }

    public static Geometry? Deserialize(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
        {
            return null;
        }
        try
        {
            return Geometry.Parse(data);
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            return null;
        }
    }
}

public enum InkExportScope
{
    AllPersistedAndSession = 0,
    SessionChangesOnly = 1
}

/// <summary>
/// Options controlling how ink annotations are exported as composite images.
/// </summary>
public sealed class InkExportOptions
{
    /// <summary>
    /// DPI used when rendering PDF pages to bitmap before compositing.
    /// Default: 150.
    /// </summary>
    public int Dpi { get; set; } = 150;

    /// <summary>
    /// Output image format. Supported: "PNG", "JPG".
    /// For single-image sources, the original format is preserved by default.
    /// </summary>
    public string Format { get; set; } = "PNG";

    /// <summary>
    /// JPEG quality (1-100). Only used when Format is "JPG".
    /// </summary>
    public int JpegQuality { get; set; } = 90;

    /// <summary>
    /// Export scope. Default exports all persisted and in-memory ink.
    /// </summary>
    public InkExportScope Scope { get; set; } = InkExportScope.AllPersistedAndSession;

    /// <summary>
    /// Maximum number of files exported concurrently in batch mode.
    /// Set to 0 or negative to use adaptive concurrency.
    /// </summary>
    public int MaxParallelFiles { get; set; } = 2;
}

internal static class InkExportScaleUtilities
{
    internal static double ResolveScale(double target, double reference, double fallback)
    {
        if (reference > 0.5)
        {
            return target / reference;
        }

        if (Math.Abs(fallback) > 0.0001)
        {
            return fallback;
        }

        return 1.0;
    }

    internal static double GetBitmapWidthDip(BitmapSource bitmap)
    {
        if (bitmap.Width > 0)
        {
            return bitmap.Width;
        }

        var dpiX = bitmap.DpiX > 0 ? bitmap.DpiX : 96.0;
        return bitmap.PixelWidth * 96.0 / dpiX;
    }

    internal static double GetBitmapHeightDip(BitmapSource bitmap)
    {
        if (bitmap.Height > 0)
        {
            return bitmap.Height;
        }

        var dpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 96.0;
        return bitmap.PixelHeight * 96.0 / dpiY;
    }
}

internal static class InkAtomicFileWriter
{
    internal static void WriteAllText(
        string path,
        string content,
        string diagnosticPrefix)
    {
        AtomicFileReplaceUtility.WriteAtomically(
            path,
            tempPath => File.WriteAllText(tempPath, content),
            onTempCleanupFailure: (tempPath, ex) =>
            {
                if (!AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    return;
                }

                Debug.WriteLine($"{diagnosticPrefix} temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });
    }

    internal static void WriteAllBytes(
        string path,
        byte[] content,
        string diagnosticPrefix)
    {
        AtomicFileReplaceUtility.WriteAtomically(
            path,
            tempPath => File.WriteAllBytes(tempPath, content),
            onTempCleanupFailure: (tempPath, ex) =>
            {
                if (!AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    return;
                }

                Debug.WriteLine($"{diagnosticPrefix} temp cleanup failed path={tempPath} ex={ex.GetType().Name} msg={ex.Message}");
            });
    }
}

internal static class InkCleanupCandidateDirectoryPolicy
{
    internal static IReadOnlyCollection<string> Resolve(
        string? baseDirectory,
        string? inkPhotoRootPath,
        IEnumerable<string>? recentFolders,
        IEnumerable<string>? favoriteFolders,
        Func<string, bool> directoryExists)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddIfValid(baseDirectory);
        AddIfValid(inkPhotoRootPath);
        AddRangeIfValid(recentFolders);
        AddRangeIfValid(favoriteFolders);
        return candidates;

        void AddRangeIfValid(IEnumerable<string>? folders)
        {
            if (folders == null)
            {
                return;
            }

            foreach (var folder in folders)
            {
                AddIfValid(folder);
            }
        }

        void AddIfValid(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (directoryExists(path))
            {
                candidates.Add(path);
            }
        }
    }
}

/// <summary>
/// Restores collection invariants after ink data crosses a JSON persistence boundary.
/// JSON may explicitly contain null collections or null array elements even though
/// the in-memory DTOs initialize those collections by default.
/// </summary>
internal static class InkPayloadNormalizer
{
    internal static InkDocumentData NormalizeDocument(InkDocumentData document)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Pages ??= new List<InkPageData>();
        document.Pages.RemoveAll(static page => page is null);
        foreach (var page in document.Pages)
        {
            NormalizePage(page);
        }

        return document;
    }

    internal static InkPageData NormalizePage(InkPageData page)
    {
        ArgumentNullException.ThrowIfNull(page);

        page.Strokes = NormalizeStrokes(page.Strokes);
        return page;
    }

    internal static List<InkStrokeData> NormalizeStrokes(List<InkStrokeData>? strokes)
    {
        if (strokes is null)
        {
            return new List<InkStrokeData>();
        }

        strokes.RemoveAll(static stroke => stroke is null);
        foreach (var stroke in strokes)
        {
            NormalizeStroke(stroke);
        }

        return strokes;
    }

    private static void NormalizeStroke(InkStrokeData stroke)
    {
        stroke.Ribbons ??= new List<InkRibbonData>();
        stroke.Ribbons.RemoveAll(static ribbon => ribbon is null);
        stroke.Blooms ??= new List<InkBloomData>();
        stroke.Blooms.RemoveAll(static bloom => bloom is null);
    }
}
