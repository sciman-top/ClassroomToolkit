using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Diagnostics;

public sealed record DiagnosticsBundleExportResult(
    bool Success,
    string BundlePath,
    string Error);

public static class DiagnosticsBundleExportService
{
    public static DiagnosticsBundleExportResult Export(DiagnosticsResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        try
        {
            IConfigurationService configuration = new ConfigurationService();
            var appDataDirectory = ResolveAppDataDirectory(configuration);
            var logsDirectory = Path.Combine(appDataDirectory, "logs");
            var bundlesDirectory = Path.Combine(logsDirectory, "diagnostics-bundles");
            Directory.CreateDirectory(bundlesDirectory);

            var bundlePath = Path.Combine(
                bundlesDirectory,
                $"diagnostics-bundle-{DateTime.Now:yyyyMMdd-HHmmss}.zip");

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

        return Directory.GetFiles(logsDirectory, "error_*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(Math.Max(0, maxCount))
            .ToArray();
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

        archive.CreateEntryFromFile(sourcePath!, entryName, CompressionLevel.Optimal);
    }

    private static void AddTextEntry(ZipArchive archive, string entryName, string content)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content ?? string.Empty);
    }
}
