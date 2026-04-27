using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ClassroomToolkit.App;
using ClassroomToolkit.Domain.Utilities;

namespace ClassroomToolkit.App.Ink;

internal static class InkExportManifestUtilities
{
    private const string ExportManifestFileName = ".ink-export.manifest.json";
    private static readonly JsonSerializerOptions ManifestJsonOptions = new()
    {
        WriteIndented = true
    };

    private static readonly ConcurrentDictionary<string, object> ManifestWriteLocks = new(StringComparer.OrdinalIgnoreCase);

    internal static string GetManifestPath(string exportDir)
    {
        return Path.Combine(exportDir, ExportManifestFileName);
    }

    internal static string GetManifestKey(string outputPath)
    {
        return Path.GetFileName(outputPath);
    }

    internal static Dictionary<string, string> LoadExportManifest(string exportDir)
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

    internal static void SaveExportManifest(string exportDir, Dictionary<string, string> manifest)
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

                var json = JsonSerializer.Serialize(merged, ManifestJsonOptions);
                AtomicFileReplaceUtility.WriteAtomically(
                    path,
                    tempPath => File.WriteAllText(tempPath, json),
                    onTempCleanupFailure: static (tempPath, cleanupEx) =>
                    {
                        if (!AppGlobalExceptionHandlingPolicy.IsNonFatal(cleanupEx))
                        {
                            return;
                        }

                        System.Diagnostics.Debug.WriteLine(
                            $"[InkExportManifestUtilities] temp cleanup failed path={tempPath} ex={cleanupEx.GetType().Name} msg={cleanupEx.Message}");
                    });
            }
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            // Ignore manifest write failures; export output is still valid.
        }
    }
}
