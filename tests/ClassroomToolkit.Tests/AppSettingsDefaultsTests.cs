using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AppSettingsDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchRollCallWindowExpectedValues()
    {
        var settings = new AppSettings();

        settings.RollCallShowId.Should().BeFalse();
        settings.RollCallShowName.Should().BeTrue();
        settings.RollCallPhotoDurationSeconds.Should().Be(3);
        settings.RollCallTimerReminderIntervalMinutes.Should().Be(5);
        settings.RollCallSpeechEnabled.Should().BeFalse();
        settings.RollCallRemoteEnabled.Should().BeFalse();
        settings.RemoteGroupSwitchKey.Should().Be("enter");
    }

    [Fact]
    public void Defaults_ShouldEnableCommonPhotoSceneCompatibilityOptions()
    {
        var settings = new AppSettings();

        settings.PhotoCrossPageDisplay.Should().BeTrue();
        settings.PhotoRememberTransform.Should().BeTrue();
        settings.PhotoInertiaProfile.Should().Be("standard");
    }
}
