using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInertiaProfileDefaultsTests
{
    [Fact]
    public void Normalize_ShouldReturnKnownProfile_WhenInputIsKnown()
    {
        PhotoInertiaProfileDefaults.Normalize("SENSITIVE").Should().Be(PhotoInertiaProfileDefaults.Sensitive);
        PhotoInertiaProfileDefaults.Normalize(" heavy ").Should().Be(PhotoInertiaProfileDefaults.Heavy);
    }

    [Fact]
    public void Normalize_ShouldFallbackToStandard_WhenInputUnknown()
    {
        PhotoInertiaProfileDefaults.Normalize("legacy").Should().Be(PhotoInertiaProfileDefaults.Standard);
        PhotoInertiaProfileDefaults.Normalize(null).Should().Be(PhotoInertiaProfileDefaults.Standard);
    }
}
