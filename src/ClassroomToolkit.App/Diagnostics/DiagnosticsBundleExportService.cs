using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Globalization;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Diagnostics;

public sealed record DiagnosticsBundleExportResult(
    bool Success,
    string BundlePath,
    string Error);

public static class DiagnosticsBundleExportService
{
    internal const long MaxOptionalSourceFileBytes = 8L * 1024 * 1024;

    public static DiagnosticsBundleExportResult Export(DiagnosticsResult result)
    {
        return Export(result, new ConfigurationService(), () => DateTime.Now);
    }

    internal static DiagnosticsBundleExportResult Export(
        DiagnosticsResult result,
        IConfigurationService configuration,
        Func<DateTime> nowProvider)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(nowProvider);

        try
        {
            var appDataDirectory = ResolveAppDataDirectory(configuration);
            var logsDirectory = Path.Combine(appDataDirectory, "logs");
            var bundlesDirectory = Path.Combine(logsDirectory, "diagnostics-bundles");
            Directory.CreateDirectory(bundlesDirectory);

            var bundlePath = ResolveUniqueBundlePath(bundlesDirectory, nowProvider);

            using var archive = ZipFile.Open(bundlePath, ZipArchiveMode.Create);
            AddFileIfExists(archive, configuration.SettingsDocumentPath, "settings/settings.json");
            AddFileIfExists(archive, configuration.SettingsIniPath, "settings/settings.ini");
            AddFileIfExists(
                archive,
                Path.Combine(logsDirectory, "startup-compatibility-latest.json"),
                "logs/startup-compatibility-latest.json");

            var recentErrorLogs = SelectRecentErrorLogs(logsDirectory, maxCount: 5);
            for (var i = 0; i < recentErrorLogs.Count; i++)
            {
                var path = recentErrorLogs[i];
                AddFileIfExists(archive, path, $"logs/{Path.GetFileName(path)}");
            }

            AddTextEntry(archive, "diagnostics/diagnostics-summary.txt", BuildSummaryText(result));
            return new DiagnosticsBundleExportResult(true, bundlePath, string.Empty);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return new DiagnosticsBundleExportResult(false, string.Empty, ex.Message);
        }
    }

    internal static string ResolveUniqueBundlePath(string bundlesDirectory, Func<DateTime> nowProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bundlesDirectory);
        ArgumentNullException.ThrowIfNull(nowProvider);

        var timestampToken = nowProvider().ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var stem = $"diagnostics-bundle-{timestampToken}";
        var candidate = Path.Combine(bundlesDirectory, $"{stem}.zip");
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        for (var index = 1; index <= 999; index++)
        {
            candidate = Path.Combine(bundlesDirectory, $"{stem}-{index:D3}.zip");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(bundlesDirectory, $"{stem}-{Guid.NewGuid():N}.zip");
    }

    internal static string ResolveAppDataDirectory(IConfigurationService configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!string.IsNullOrWhiteSpace(configuration.SettingsDocumentPath))
        {
            var parent = Path.GetDirectoryName(configuration.SettingsDocumentPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                return parent;
            }
        }

        if (!string.IsNullOrWhiteSpace(configuration.SettingsIniPath))
        {
            var parent = Path.GetDirectoryName(configuration.SettingsIniPath);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                return parent;
            }
        }

        return configuration.BaseDirectory;
    }

    internal static IReadOnlyList<string> SelectRecentErrorLogs(string logsDirectory, int maxCount)
    {
        if (string.IsNullOrWhiteSpace(logsDirectory) || !Directory.Exists(logsDirectory))
        {
            return Array.Empty<string>();
        }

        var take = Math.Max(0, maxCount);
        if (take == 0)
        {
            return Array.Empty<string>();
        }

        try
        {
            var candidates = new List<(string Path, DateTime LastWriteTimeUtc)>();
            foreach (var path in Directory.EnumerateFiles(logsDirectory, "error_*.log", SearchOption.TopDirectoryOnly))
            {
                if (TryGetLastWriteTimeUtc(path, out var lastWriteTimeUtc))
                {
                    candidates.Add((path, lastWriteTimeUtc));
                }
            }

            return candidates
                .OrderByDescending(candidate => candidate.LastWriteTimeUtc)
                .Take(take)
                .Select(candidate => candidate.Path)
                .ToArray();
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                $"[DiagnosticsBundleExport] SelectRecentErrorLogs failed. directory='{logsDirectory}', reason={ex.GetType().Name}:{ex.Message}");
            return Array.Empty<string>();
        }
    }

    private static string BuildSummaryText(DiagnosticsResult result)
    {
        return string.Join(
            Environment.NewLine,
            new[]
            {
                result.Title,
                result.Summary,
                string.Empty,
                result.Detail ?? string.Empty,
                string.Empty,
                result.Suggestion ?? string.Empty
            });
    }

    private static void AddFileIfExists(ZipArchive archive, string? sourcePath, string entryName)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return;
        }

        if (!IsAllowedBundleEntryName(entryName))
        {
            Debug.WriteLine(
                $"[DiagnosticsBundleExport] Skip unexpected entry '{entryName}' from '{sourcePath}'.");
            return;
        }

        if (!TryGetFileLength(sourcePath, out var fileLength))
        {
            return;
        }

        if (fileLength > MaxOptionalSourceFileBytes)
        {
            Debug.WriteLine(
                $"[DiagnosticsBundleExport] Skip oversized file '{sourcePath}' ({fileLength} bytes) for entry '{entryName}'.");
            return;
        }

        try
        {
            archive.CreateEntryFromFile(sourcePath!, entryName, CompressionLevel.Optimal);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                $"[DiagnosticsBundleExport] Skip file '{sourcePath}' while creating entry '{entryName}': {ex.GetType().Name} - {ex.Message}");
        }
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content ?? string.Empty);
    }

    private static bool TryGetLastWriteTimeUtc(string path, out DateTime writeTimeUtc)
    {
        writeTimeUtc = default;
        try
        {
            writeTimeUtc = File.GetLastWriteTimeUtc(path);
            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                $"[DiagnosticsBundleExport] Skip log timestamp read. path='{path}', reason={ex.GetType().Name}:{ex.Message}");
            return false;
        }
    }

    internal static bool IsAllowedBundleEntryName(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName))
        {
            return false;
        }

        var normalized = entryName.Replace('\\', '/');
        if (normalized.Equals("settings/settings.json", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("settings/settings.ini", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("logs/startup-compatibility-latest.json", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("diagnostics/diagnostics-summary.txt", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!normalized.StartsWith("logs/error_", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalized.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryGetFileLength(string path, out long length)
    {
        length = 0;
        try
        {
            length = new FileInfo(path).Length;
            return true;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                $"[DiagnosticsBundleExport] Skip file length read. path='{path}', reason={ex.GetType().Name}:{ex.Message}");
            return false;
        }
    }
}
