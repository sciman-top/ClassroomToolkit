using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowSaveSettingsFlowContractTests
{
    [Fact]
    public void SaveSettings_ShouldCaptureStylusAdaptiveStateBeforePersisting()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (_overlayWindow != null &&");
        source.Should().Contain("_overlayWindow.TryGetStylusAdaptiveState(");
        source.Should().Contain("_settings.StylusAdaptivePressureProfile = pressureProfile;");
        source.Should().Contain("_settings.StylusAdaptiveSampleRateTier = sampleRateTier;");
    }

    [Fact]
    public void SaveSettings_ShouldMarkSuccessAndUseNotificationPlan_OnFailure()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_settingsService.Save(_settings);");
        source.Should().Contain("SettingsSaveFailureNotificationStateUpdater.MarkSaveSucceeded(ref _settingsSaveFailedNotified);");
        source.Should().Contain("var notificationPlan = SettingsSaveFailureNotificationPolicy.Resolve(_settingsSaveFailedNotified);");
        source.Should().Contain("SettingsSaveFailureNotificationStateUpdater.ApplyNotificationPlan(");
        source.Should().Contain("if (!notificationPlan.ShouldNotify)");
    }

    [Fact]
    public void SaveSettings_ShouldUseMainInfoSafeMessage_ForFailureDetails()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var detail = $\"设置保存失败：{ex.Message}");
        source.Should().Contain("ShowMainInfoMessageSafe(\"settings-save-failed\", detail);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
