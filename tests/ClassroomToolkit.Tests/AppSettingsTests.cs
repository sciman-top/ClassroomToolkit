using ClassroomToolkit.App.Settings;
using FluentAssertions;
using MediaColors = System.Windows.Media.Colors;

namespace ClassroomToolkit.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void ParseColor_ShouldReturnParsedColor_WhenInputValid()
    {
        var result = AppSettings.ParseColor("#FF0000", MediaColors.Blue);

        result.Should().Be(MediaColors.Red);
    }

    [Fact]
    public void ParseColor_ShouldReturnFallback_WhenInputInvalid()
    {
        var fallback = MediaColors.Blue;

        var result = AppSettings.ParseColor("not-a-color", fallback);

        result.Should().Be(fallback);
    }
}
