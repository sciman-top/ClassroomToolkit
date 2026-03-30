using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivationSurfacePolicyTests
{
    [Theory]
    [InlineData(true, ZOrderSurface.PhotoFullscreen, true)]
    [InlineData(true, ZOrderSurface.Whiteboard, true)]
    [InlineData(true, ZOrderSurface.PresentationFullscreen, false)]
    [InlineData(false, ZOrderSurface.PhotoFullscreen, false)]
    public void Resolve_ShouldMatchExpected(bool overlayVisible, ZOrderSurface surface, bool expected)
    {
        var decision = OverlayActivationSurfacePolicy.Resolve(overlayVisible, surface);
        decision.ShouldActivate.Should().Be(expected);
    }

    [Fact]
    public void ShouldActivate_ShouldMapResolveDecision()
    {
        OverlayActivationSurfacePolicy.ShouldActivate(true, ZOrderSurface.Whiteboard).Should().BeTrue();
    }
}
