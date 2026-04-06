using ClassroomToolkit.App.Paint;
using FluentAssertions;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.Tests;

public sealed class PhotoZoomAnchorPolicyTests
{
    [Theory]
    [InlineData(1920, 1080, 960, 540)]
    [InlineData(1366, 768, 683, 384)]
    [InlineData(1, 1, 0.5, 0.5)]
    public void ResolveViewportCenter_ShouldReturnViewportMidpoint(
        double width,
        double height,
        double expectedX,
        double expectedY)
    {
        PhotoZoomAnchorPolicy.ResolveViewportCenter(width, height)
            .Should()
            .Be(new WpfPoint(expectedX, expectedY));
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1, 1080)]
    public void ResolveViewportCenter_ShouldReturnDefault_WhenViewportIsInvalid(
        double width,
        double height)
    {
        PhotoZoomAnchorPolicy.ResolveViewportCenter(width, height)
            .Should()
            .Be(default(WpfPoint));
    }
}
