using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WindowDipToScreenRectPolicyTests
{
    [Fact]
    public void ResolveFromDip_ShouldReturnExpectedPixels_WhenScaleIsOne()
    {
        var rect = WindowDipToScreenRectPolicy.ResolveFromDip(
            leftDip: 100,
            topDip: 60,
            widthDip: 200,
            heightDip: 48,
            dpiScaleX: 1.0,
            dpiScaleY: 1.0);

        rect.Left.Should().Be(100);
        rect.Top.Should().Be(60);
        rect.Width.Should().Be(200);
        rect.Height.Should().Be(48);
    }

    [Fact]
    public void ResolveFromDip_ShouldScaleBounds_WhenScaleIsOnePointFive()
    {
        var rect = WindowDipToScreenRectPolicy.ResolveFromDip(
            leftDip: 100,
            topDip: 60,
            widthDip: 200,
            heightDip: 48,
            dpiScaleX: 1.5,
            dpiScaleY: 1.5);

        rect.Left.Should().Be(150);
        rect.Top.Should().Be(90);
        rect.Width.Should().Be(300);
        rect.Height.Should().Be(72);
    }

    [Fact]
    public void ResolveFromDip_ShouldFallbackToScaleOne_WhenScaleIsInvalid()
    {
        var rect = WindowDipToScreenRectPolicy.ResolveFromDip(
            leftDip: 12.4,
            topDip: 8.6,
            widthDip: 0,
            heightDip: 0,
            dpiScaleX: 0,
            dpiScaleY: -1);

        rect.Left.Should().Be(12);
        rect.Top.Should().Be(8);
        rect.Width.Should().Be(1);
        rect.Height.Should().Be(1);
    }
}
