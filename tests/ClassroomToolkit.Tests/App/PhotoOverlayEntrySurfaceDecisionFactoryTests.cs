using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoOverlayEntrySurfaceDecisionFactoryTests
{
    [Fact]
    public void Resolve_ShouldTouchPhotoSurface_WhenRequested()
    {
        var decision = PhotoOverlayEntrySurfaceDecisionFactory.Resolve(touchPhotoSurface: true);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldReturnNoTouch_WhenNotRequested()
    {
        var decision = PhotoOverlayEntrySurfaceDecisionFactory.Resolve(touchPhotoSurface: false);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}
