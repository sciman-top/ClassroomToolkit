using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

internal static class InkExportSnapshotBuilder
{
    internal static bool HasAnyInkStrokes(InkDocumentData? inkDoc)
    {
        if (inkDoc?.Pages == null || inkDoc.Pages.Count == 0)
        {
            return false;
        }

        foreach (var page in inkDoc.Pages)
        {
            if (page.Strokes?.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    internal static void ApplyScopeFilter(
        InkDocumentData inkDoc,
        InkExportScope scope,
        Func<InkPageData, bool> keepPage)
    {
        if (scope != InkExportScope.SessionChangesOnly)
        {
            return;
        }
        if (inkDoc.Pages.Count == 0)
        {
            return;
        }

        inkDoc.Pages = inkDoc.Pages.Where(keepPage).ToList();
    }

    internal static void UpsertPageStrokes(
        InkDocumentData inkDoc,
        string sourceFilePath,
        int pageIndex,
        List<InkStrokeData> strokes)
    {
        var currentPage = inkDoc.Pages.Find(p => p.PageIndex == pageIndex);
        if (strokes.Count == 0)
        {
            if (currentPage != null)
            {
                inkDoc.Pages.Remove(currentPage);
            }
            return;
        }

        if (currentPage == null)
        {
            currentPage = new InkPageData
            {
                PageIndex = pageIndex,
                SourcePath = sourceFilePath,
                DocumentName = Path.GetFileNameWithoutExtension(sourceFilePath)
            };
            inkDoc.Pages.Add(currentPage);
        }

        currentPage.Strokes = strokes;
        currentPage.UpdatedAt = DateTime.UtcNow;
    }

    internal static void MergeCachedPages(
        InkDocumentData inkDoc,
        string sourceFilePath,
        IReadOnlyList<(string Key, List<InkStrokeData> Strokes)> cacheSnapshot,
        Func<IEnumerable<InkStrokeData>, List<InkStrokeData>> cloneStrokes)
    {
        foreach (var entry in cacheSnapshot)
        {
            if (!TryParseCacheKey(entry.Key, out var cachedSourcePath, out var pageIndex))
            {
                continue;
            }
            if (!string.Equals(cachedSourcePath, sourceFilePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            UpsertPageStrokes(inkDoc, sourceFilePath, pageIndex, cloneStrokes(entry.Strokes ?? new List<InkStrokeData>()));
        }
    }

    internal static InkDocumentData? CloneDocument(
        InkDocumentData? source,
        Func<IEnumerable<InkStrokeData>, List<InkStrokeData>> cloneStrokes)
    {
        if (source == null)
        {
            return null;
        }

        var clone = new InkDocumentData
        {
            Version = source.Version,
            SourcePath = source.SourcePath
        };

        foreach (var page in source.Pages)
        {
            clone.Pages.Add(new InkPageData
            {
                PageIndex = page.PageIndex,
                DocumentName = page.DocumentName,
                SourcePath = page.SourcePath,
                BackgroundImageFile = page.BackgroundImageFile,
                CreatedAt = page.CreatedAt,
                UpdatedAt = page.UpdatedAt,
                Strokes = cloneStrokes(page.Strokes ?? new List<InkStrokeData>())
            });
        }

        return clone;
    }

    internal static IEnumerable<string> EnumerateCachedInkSourcesInDirectory(
        IReadOnlyList<(string Key, List<InkStrokeData> Strokes)> cacheSnapshot,
        string directoryPath)
    {
        foreach (var entry in cacheSnapshot)
        {
            if (entry.Strokes == null || entry.Strokes.Count == 0)
            {
                continue;
            }
            if (!TryParseCacheKey(entry.Key, out var sourcePath, out _))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }
            if (!string.Equals(Path.GetDirectoryName(sourcePath), directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            yield return sourcePath;
        }
    }

    internal static bool TryParseCacheKey(string cacheKey, out string sourcePath, out int pageIndex)
    {
        sourcePath = string.Empty;
        pageIndex = 1;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        if (cacheKey.StartsWith("img|", StringComparison.Ordinal))
        {
            sourcePath = cacheKey["img|".Length..];
            pageIndex = 1;
            return !string.IsNullOrWhiteSpace(sourcePath);
        }

        if (cacheKey.StartsWith("pdf|", StringComparison.Ordinal))
        {
            const string marker = "|page_";
            var markerIndex = cacheKey.LastIndexOf(marker, StringComparison.Ordinal);
            if (markerIndex <= "pdf|".Length)
            {
                return false;
            }

            var pathPart = cacheKey["pdf|".Length..markerIndex];
            var pagePart = cacheKey[(markerIndex + marker.Length)..];
            if (!int.TryParse(pagePart, out pageIndex) || pageIndex <= 0)
            {
                return false;
            }

            sourcePath = pathPart;
            return !string.IsNullOrWhiteSpace(sourcePath);
        }

        return false;
    }
}
