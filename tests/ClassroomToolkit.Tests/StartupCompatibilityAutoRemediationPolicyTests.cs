using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StartupCompatibilityAutoRemediationPolicyTests
{
    [Fact]
    public void Apply_ShouldSwitchToMessageModes_WhenArchitectureMismatchDetected()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue(
                    "presentation-arch-mismatch",
                    "arch mismatch",
                    "unify x64",
                    false)
            });
        var settings = new AppSettings
        {
            OfficeInputMode = WpsInputModeDefaults.Raw,
            WpsInputMode = WpsInputModeDefaults.Auto,
            PresentationLockStrategyWhenDegraded = false
        };

        var result = StartupCompatibilityAutoRemediationPolicy.Apply(report, settings, null);

        result.HasChanges.Should().BeTrue();
        result.HasSettingsChanges.Should().BeTrue();
        result.AppliedActions.Should().HaveCount(3);
        settings.OfficeInputMode.Should().Be(WpsInputModeDefaults.Message);
        settings.WpsInputMode.Should().Be(WpsInputModeDefaults.Message);
        settings.PresentationLockStrategyWhenDegraded.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldBeNoOp_WhenSettingsAlreadyConservative()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue(
                    "presentation-privilege-unknown",
                    "privilege unknown",
                    "check security software",
                    false)
            });
        var settings = new AppSettings
        {
            OfficeInputMode = WpsInputModeDefaults.Message,
            WpsInputMode = WpsInputModeDefaults.Message,
            PresentationLockStrategyWhenDegraded = true
        };

        var result = StartupCompatibilityAutoRemediationPolicy.Apply(report, settings, null);

        result.HasChanges.Should().BeFalse();
        result.HasSettingsChanges.Should().BeFalse();
        result.AppliedActions.Should().BeEmpty();
    }

    [Fact]
    public void Apply_ShouldIgnoreUnrelatedWarnings()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue(
                    "native-pdfium-missing",
                    "missing pdfium",
                    "reinstall package",
                    false)
            });
        var settings = new AppSettings
        {
            OfficeInputMode = WpsInputModeDefaults.Auto,
            WpsInputMode = WpsInputModeDefaults.Message,
            PresentationLockStrategyWhenDegraded = true
        };

        var result = StartupCompatibilityAutoRemediationPolicy.Apply(report, settings, null);

        result.HasChanges.Should().BeFalse();
        result.HasSettingsChanges.Should().BeFalse();
        result.AppliedActions.Should().BeEmpty();
        settings.OfficeInputMode.Should().Be(WpsInputModeDefaults.Auto);
        settings.WpsInputMode.Should().Be(WpsInputModeDefaults.Message);
        settings.PresentationLockStrategyWhenDegraded.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldCreateSettingsDirectory_WhenSettingsDirectoryMissingDetected()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue(
                    "settings-dir-missing",
                    "missing settings dir",
                    "create dir",
                    false)
            });
        var root = Path.Combine(Path.GetTempPath(), $"ctoolkit-settings-{Guid.NewGuid():N}");
        var settingsPath = Path.Combine(root, "sub", "settings.json");
        var settings = new AppSettings();

        try
        {
            var result = StartupCompatibilityAutoRemediationPolicy.Apply(report, settings, settingsPath);

            result.HasChanges.Should().BeTrue();
            result.HasSettingsChanges.Should().BeFalse();
            Directory.Exists(Path.GetDirectoryName(settingsPath)!).Should().BeTrue();
            result.AppliedActions.Should().ContainSingle(action => action.Contains("已自动创建设置目录"));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
