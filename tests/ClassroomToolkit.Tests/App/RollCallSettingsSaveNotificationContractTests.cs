using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallSettingsSaveNotificationContractTests
{
    [Fact]
    public void RollCallWindowState_ShouldUseSettingsSaveFailurePolicy()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SettingsSaveFailureNotificationPolicy.Resolve(_settingsSaveFailedNotified)");
        source.Should().Contain("SettingsSaveFailureNotificationStateUpdater.ApplyNotificationPlan(");
    }

    [Fact]
    public void RollCallWindowState_ShouldResetSettingsSaveFailureState_OnSuccessfulSave()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SettingsSaveFailureNotificationStateUpdater.MarkSaveSucceeded(ref _settingsSaveFailedNotified);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.State.cs");
    }
}
