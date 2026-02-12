using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallSettingsApplierTests
{
    [Fact]
    public void Apply_ShouldUpdateRemoteGroupSwitchFields()
    {
        var settings = new AppSettings
        {
            RollCallRemoteGroupSwitchEnabled = false,
            RemoteGroupSwitchKey = "b",
            RollCallRemoteEnabled = false,
            RemotePresenterKey = "tab"
        };
        var patch = new RollCallSettingsPatch(
            RollCallShowId: true,
            RollCallShowName: true,
            RollCallShowPhoto: false,
            RollCallPhotoDurationSeconds: 0,
            RollCallPhotoSharedClass: string.Empty,
            RollCallTimerSoundEnabled: true,
            RollCallTimerReminderEnabled: false,
            RollCallTimerReminderIntervalMinutes: 3,
            RollCallTimerSoundVariant: "gentle",
            RollCallTimerReminderSoundVariant: "soft_beep",
            RollCallSpeechEnabled: false,
            RollCallSpeechEngine: "pyttsx3",
            RollCallSpeechVoiceId: string.Empty,
            RollCallSpeechOutputId: string.Empty,
            RollCallRemoteEnabled: true,
            RollCallRemoteGroupSwitchEnabled: true,
            RemotePresenterKey: "f5",
            RemoteGroupSwitchKey: "b");

        RollCallSettingsApplier.Apply(settings, patch);

        settings.RollCallRemoteEnabled.Should().BeTrue();
        settings.RemotePresenterKey.Should().Be("f5");
        settings.RollCallRemoteGroupSwitchEnabled.Should().BeTrue();
        settings.RemoteGroupSwitchKey.Should().Be("b");
    }
}
