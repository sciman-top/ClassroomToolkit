using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoOverlayEntrySurfaceTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldTouchPhotoSurface_WhenEntryPlanRequestsPhotoSurface()
    {
        var decision = PhotoOverlayEntrySurfaceTransitionPolicy.Resolve(touchPhotoSurface: true);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRemainNoop_WhenEntryPlanDoesNotTouchPhotoSurface()
    {
        var decision = PhotoOverlayEntrySurfaceTransitionPolicy.Resolve(touchPhotoSurface: false);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}
