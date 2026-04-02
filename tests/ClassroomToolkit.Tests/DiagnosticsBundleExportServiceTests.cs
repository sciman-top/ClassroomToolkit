using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class DiagnosticsBundleExportServiceTests
{
    [Fact]
    public void ResolveAppDataDirectory_ShouldPreferSettingsDocumentDirectory()
    {
        var configuration = new FakeConfigurationService(
            baseDirectory: @"C:\base",
            settingsIniPath: @"C:\ini\settings.ini",
            settingsDocumentPath: @"C:\json\settings.json");

        var result = DiagnosticsBundleExportService.ResolveAppDataDirectory(configuration);

        result.Should().Be(@"C:\json");
    }

    [Fact]
    public void SelectRecentErrorLogs_ShouldReturnLatestFilesWithinLimit()
    {
        var tempDir = TestPathHelper.CreateDirectory("ctool_diag_bundle_logs");
        try
        {
            var oldLog = Path.Combine(tempDir, "error_20260101.log");
            var newLog = Path.Combine(tempDir, "error_20260102.log");
            var other = Path.Combine(tempDir, "anything.log");

            File.WriteAllText(oldLog, "old");
            File.WriteAllText(newLog, "new");
            File.WriteAllText(other, "other");

            File.SetLastWriteTimeUtc(oldLog, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newLog, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));

            var result = DiagnosticsBundleExportService.SelectRecentErrorLogs(tempDir, maxCount: 1);

            result.Should().ContainSingle();
            result[0].Should().Be(newLog);
        }
        finally
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best-effort cleanup in test environment.
            }
        }
    }

    private sealed class FakeConfigurationService : IConfigurationService
    {
        public FakeConfigurationService(
            string baseDirectory,
            string settingsIniPath,
            string settingsDocumentPath)
        {
            BaseDirectory = baseDirectory;
            SettingsIniPath = settingsIniPath;
            SettingsDocumentPath = settingsDocumentPath;
        }

        public string BaseDirectory { get; }

        public string SettingsIniPath { get; }

        public SettingsDocumentFormat SettingsDocumentFormat => SettingsDocumentFormat.Json;

        public string SettingsDocumentPath { get; }
    }
}
