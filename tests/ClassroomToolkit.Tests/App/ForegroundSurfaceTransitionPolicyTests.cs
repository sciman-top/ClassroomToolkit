using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ForegroundSurfaceTransitionPolicyTests
{
    [Fact]
    public void ResolveOverlayActivated_ShouldSuppress_WhenFlagged()
    {
        var decision = ForegroundSurfaceTransitionPolicy.Resolve(
            ForegroundSurfaceTransitionKind.OverlayActivated,
            suppressNextApply: true,
            overlayExists: true,
            photoModeActive: true,
            whiteboardActive: false,
            surface: ZOrderSurface.None);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolveOverlayActivated_ShouldTouchPhotoSurface_WhenPhotoModeActive()
    {
        var decision = ForegroundSurfaceTransitionPolicy.Resolve(
            ForegroundSurfaceTransitionKind.OverlayActivated,
            suppressNextApply: false,
            overlayExists: true,
            photoModeActive: true,
            whiteboardActive: false,
            surface: ZOrderSurface.None);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void ResolveOverlayActivated_ShouldTouchWhiteboardSurface_WhenWhiteboardActive()
    {
        var decision = ForegroundSurfaceTransitionPolicy.Resolve(
            ForegroundSurfaceTransitionKind.OverlayActivated,
            suppressNextApply: false,
            overlayExists: true,
            photoModeActive: false,
            whiteboardActive: true,
            surface: ZOrderSurface.None);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.Whiteboard);
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void ResolveExplicitForeground_ShouldTouchSurface_WhenOverlayExists()
    {
        var decision = ForegroundSurfaceTransitionPolicy.Resolve(
            ForegroundSurfaceTransitionKind.ExplicitForeground,
            overlayExists: true,
            surface: ZOrderSurface.PresentationFullscreen,
            suppressNextApply: false,
            photoModeActive: false,
            whiteboardActive: false);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.PresentationFullscreen);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void ResolveExplicitForeground_ShouldSkip_WhenOverlayMissing()
    {
        var decision = ForegroundSurfaceTransitionPolicy.Resolve(
            ForegroundSurfaceTransitionKind.ExplicitForeground,
            overlayExists: false,
            surface: ZOrderSurface.PhotoFullscreen,
            suppressNextApply: false,
            photoModeActive: false,
            whiteboardActive: false);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_WithActivityStateOverload_ShouldMatchPhotoModeDecision()
    {
        var activity = new ForegroundSurfaceActivityState(
            OverlayExists: true,
            PhotoModeActive: true,
            WhiteboardActive: false);

        var decision = ForegroundSurfaceTransitionPolicy.Resolve(
            ForegroundSurfaceTransitionKind.OverlayActivated,
            suppressNextApply: false,
            activity,
            surface: ZOrderSurface.None);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
    }
}
