using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClassroomToolkit.App.Ink;

public sealed partial class InkExportService
{
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

    private static string[] GetFilesSafely(string directoryPath)
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
}
