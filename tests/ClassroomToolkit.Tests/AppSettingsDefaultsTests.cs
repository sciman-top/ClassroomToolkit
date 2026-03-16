using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AppSettingsDefaultsTests
{
    [Fact]
    public void Defaults_ShouldEnableCommonPhotoSceneCompatibilityOptions()
    {
        var settings = new AppSettings();

        settings.PhotoCrossPageDisplay.Should().BeTrue();
        settings.PhotoRememberTransform.Should().BeTrue();
    }
}
