using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class MainWindowSettingsSaveNotificationContractTests
{
    [Fact]
    public void MainWindow_ShouldUseSettingsSaveFailurePolicy_InSaveSettings()
    {
        var source = MainWindowContractSourceReader.ReadCombinedSource();

        source.Should().Contain("SettingsSaveFailureNotificationPolicy.Resolve(_settingsSaveFailedNotified)");
        source.Should().Contain("SettingsSaveFailureNotificationStateUpdater.ApplyNotificationPlan(");
    }

    [Fact]
    public void MainWindow_ShouldResetSettingsSaveFailureState_OnSuccessfulSave()
    {
        var source = MainWindowContractSourceReader.ReadCombinedSource();

        source.Should().Contain("SettingsSaveFailureNotificationStateUpdater.MarkSaveSucceeded(ref _settingsSaveFailedNotified);");
    }
}
