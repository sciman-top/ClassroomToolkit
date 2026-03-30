using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PhotoModeSurfaceTransitionPolicyTests
{
    [Fact]
    public void ResolvePhotoModeChanged_ShouldNotForceByDefault()
    {
        var activeDecision = PhotoModeSurfaceTransitionPolicy.Resolve(
            PhotoModeSurfaceTransitionKind.PhotoModeChanged,
            photoModeActive: true,
            requestZOrderApply: true,
            forceEnforceZOrder: false,
            overlayVisible: false);
        var inactiveDecision = PhotoModeSurfaceTransitionPolicy.Resolve(
            PhotoModeSurfaceTransitionKind.PhotoModeChanged,
            photoModeActive: false,
            requestZOrderApply: true,
            forceEnforceZOrder: false,
            overlayVisible: false);

        activeDecision.ShouldTouchSurface.Should().BeTrue();
        activeDecision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        activeDecision.RequestZOrderApply.Should().BeTrue();
        activeDecision.ForceEnforceZOrder.Should().BeFalse();

        inactiveDecision.ShouldTouchSurface.Should().BeFalse();
        inactiveDecision.Surface.Should().Be(ZOrderSurface.None);
        inactiveDecision.RequestZOrderApply.Should().BeTrue();
        inactiveDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePhotoModeChanged_ShouldHonorExplicitForce_WhenPhotoModeInactive()
    {
        var decision = PhotoModeSurfaceTransitionPolicy.Resolve(
            PhotoModeSurfaceTransitionKind.PhotoModeChanged,
            photoModeActive: false,
            requestZOrderApply: true,
            forceEnforceZOrder: true,
            overlayVisible: false);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void ResolvePresentationFullscreenDetected_ShouldFollowOverlayVisibility()
    {
        var visibleDecision = PhotoModeSurfaceTransitionPolicy.Resolve(
            PhotoModeSurfaceTransitionKind.PresentationFullscreenDetected,
            overlayVisible: true,
            photoModeActive: false,
            requestZOrderApply: false,
            forceEnforceZOrder: false);
        var hiddenDecision = PhotoModeSurfaceTransitionPolicy.Resolve(
            PhotoModeSurfaceTransitionKind.PresentationFullscreenDetected,
            overlayVisible: false,
            photoModeActive: false,
            requestZOrderApply: false,
            forceEnforceZOrder: false);

        visibleDecision.ShouldTouchSurface.Should().BeFalse();
        visibleDecision.RequestZOrderApply.Should().BeTrue();
        visibleDecision.ForceEnforceZOrder.Should().BeTrue();

        hiddenDecision.ShouldTouchSurface.Should().BeFalse();
        hiddenDecision.RequestZOrderApply.Should().BeFalse();
        hiddenDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldFallbackToNoTouch_ForUnknownKind()
    {
        var decision = PhotoModeSurfaceTransitionPolicy.Resolve(
            (PhotoModeSurfaceTransitionKind)999,
            photoModeActive: true,
            requestZOrderApply: true,
            forceEnforceZOrder: true,
            overlayVisible: true);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ContextOverload_ShouldMatchPrimitiveBehavior()
    {
        var context = new PhotoModeSurfaceTransitionContext(
            PhotoModeActive: true,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false,
            OverlayVisible: false);

        var decision = PhotoModeSurfaceTransitionPolicy.Resolve(
            PhotoModeSurfaceTransitionKind.PhotoModeChanged,
            context);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}
