using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Settings;
using FluentAssertions;
using System.IO.Compression;

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

    [Fact]
    public void Export_ShouldUseUniqueBundlePath_WhenTimestampCollides()
    {
        var tempDir = TestPathHelper.CreateDirectory("ctool_diag_bundle_unique");
        try
        {
            var settingsJsonPath = Path.Combine(tempDir, "settings.json");
            var settingsIniPath = Path.Combine(tempDir, "settings.ini");
            File.WriteAllText(settingsJsonPath, "{}");
            File.WriteAllText(settingsIniPath, "[settings]");
            var configuration = new FakeConfigurationService(tempDir, settingsIniPath, settingsJsonPath);
            var fixedNow = new DateTime(2026, 4, 22, 12, 34, 56);
            var diagnostics = CreateDiagnosticsResult();

            var first = DiagnosticsBundleExportService.Export(diagnostics, configuration, () => fixedNow);
            var second = DiagnosticsBundleExportService.Export(diagnostics, configuration, () => fixedNow);

            first.Success.Should().BeTrue(first.Error);
            second.Success.Should().BeTrue(second.Error);
            first.BundlePath.Should().NotBe(second.BundlePath);
            File.Exists(first.BundlePath).Should().BeTrue();
            File.Exists(second.BundlePath).Should().BeTrue();
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

    [Fact]
    public void Export_ShouldSucceed_WhenOneOptionalFileIsLocked()
    {
        var tempDir = TestPathHelper.CreateDirectory("ctool_diag_bundle_locked");
        try
        {
            var settingsJsonPath = Path.Combine(tempDir, "settings.json");
            var settingsIniPath = Path.Combine(tempDir, "settings.ini");
            File.WriteAllText(settingsJsonPath, "{\"ok\":true}");
            File.WriteAllText(settingsIniPath, "[settings]");
            var configuration = new FakeConfigurationService(tempDir, settingsIniPath, settingsJsonPath);
            var diagnostics = CreateDiagnosticsResult();

            using var heldLock = new FileStream(settingsJsonPath, FileMode.Open, FileAccess.Read, FileShare.None);
            var export = DiagnosticsBundleExportService.Export(
                diagnostics,
                configuration,
                () => new DateTime(2026, 4, 22, 13, 15, 0));

            export.Success.Should().BeTrue(export.Error);
            File.Exists(export.BundlePath).Should().BeTrue();
            using var archive = ZipFile.OpenRead(export.BundlePath);
            archive.Entries.Should().Contain(entry => entry.FullName == "settings/settings.ini");
            archive.Entries.Should().Contain(entry => entry.FullName == "diagnostics/diagnostics-summary.txt");
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

    [Fact]
    public void Export_ShouldSkipOversizedOptionalFile_AndRemainSuccessful()
    {
        var tempDir = TestPathHelper.CreateDirectory("ctool_diag_bundle_oversized");
        try
        {
            var logsDir = Path.Combine(tempDir, "logs");
            Directory.CreateDirectory(logsDir);
            var settingsJsonPath = Path.Combine(tempDir, "settings.json");
            var settingsIniPath = Path.Combine(tempDir, "settings.ini");
            var startupCompatibilityPath = Path.Combine(logsDir, "startup-compatibility-latest.json");
            File.WriteAllText(settingsJsonPath, "{\"ok\":true}");
            File.WriteAllText(settingsIniPath, "[settings]");
            File.WriteAllBytes(
                startupCompatibilityPath,
                new byte[DiagnosticsBundleExportService.MaxOptionalSourceFileBytes + 1024]);
            var configuration = new FakeConfigurationService(tempDir, settingsIniPath, settingsJsonPath);

            var export = DiagnosticsBundleExportService.Export(
                CreateDiagnosticsResult(),
                configuration,
                () => new DateTime(2026, 4, 22, 14, 0, 0));

            export.Success.Should().BeTrue(export.Error);
            using var archive = ZipFile.OpenRead(export.BundlePath);
            archive.Entries.Should().Contain(entry => entry.FullName == "settings/settings.json");
            archive.Entries.Should().Contain(entry => entry.FullName == "settings/settings.ini");
            archive.Entries.Should().Contain(entry => entry.FullName == "diagnostics/diagnostics-summary.txt");
            archive.Entries.Should().NotContain(entry => entry.FullName == "logs/startup-compatibility-latest.json");
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

    [Theory]
    [InlineData("settings/settings.json", true)]
    [InlineData("settings/settings.ini", true)]
    [InlineData("logs/startup-compatibility-latest.json", true)]
    [InlineData("diagnostics/diagnostics-summary.txt", true)]
    [InlineData("logs/error_20260422.log", true)]
    [InlineData("logs/app_20260422.log", false)]
    [InlineData("tmp/anything.txt", false)]
    public void IsAllowedBundleEntryName_ShouldMatchPolicy(string entryName, bool expected)
    {
        DiagnosticsBundleExportService.IsAllowedBundleEntryName(entryName).Should().Be(expected);
    }

    private static DiagnosticsResult CreateDiagnosticsResult()
    {
        return new DiagnosticsResult(
            HasIssues: true,
            Title: "诊断标题",
            Detail: "诊断详情",
            Suggestion: "诊断建议",
            HealthBadge: "WARN");
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
