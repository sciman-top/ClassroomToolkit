using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Ink;

public sealed partial class InkExportService
{
    /// <summary>
    /// List source files in the given directory that already have Method B composite outputs.
    /// </summary>
    public IReadOnlyList<string> ListFilesWithCompositeExports(string directoryPath)
    {
        return ListCompositeSourceFilesInDirectory(directoryPath);
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

            if (isPdf && (!TryGetPdfPageIndexFromCompositeName(fileName, out var outputPageIndex) || outputPageIndex != pageIndex))
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

    private static string BuildOutputPath(string sourcePath, int pageIndex, bool isPdf, InkExportOptions options)
    {
        var sourceDir = Path.GetDirectoryName(sourcePath) ?? string.Empty;
        var exportDir = Path.Combine(sourceDir, ExportFolderName);
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);

        string extension;
        if (isPdf)
        {
            extension = options.Format.Equals("JPG", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
        }
        else
        {
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
}
