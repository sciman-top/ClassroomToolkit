using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerSurfaceTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldTouchImageManagerWithoutForce_WhenOpenAndOverlayVisible()
    {
        var decision = ImageManagerSurfaceTransitionPolicy.Resolve(
            ImageManagerSurfaceTransitionKind.Open,
            overlayVisible: true);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.ImageManager);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldTouchImageManagerWithoutForce_WhenActivated()
    {
        var visibleDecision = ImageManagerSurfaceTransitionPolicy.Resolve(
            ImageManagerSurfaceTransitionKind.Activated,
            overlayVisible: true);
        var hiddenDecision = ImageManagerSurfaceTransitionPolicy.Resolve(
            ImageManagerSurfaceTransitionKind.Activated,
            overlayVisible: false);

        visibleDecision.ShouldTouchSurface.Should().BeTrue();
        visibleDecision.Surface.Should().Be(ZOrderSurface.ImageManager);
        visibleDecision.RequestZOrderApply.Should().BeTrue();
        visibleDecision.ForceEnforceZOrder.Should().BeFalse();

        hiddenDecision.ShouldTouchSurface.Should().BeTrue();
        hiddenDecision.Surface.Should().Be(ZOrderSurface.ImageManager);
        hiddenDecision.RequestZOrderApply.Should().BeTrue();
        hiddenDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestApplyWithoutTouch_WhenClosed()
    {
        var visibleDecision = ImageManagerSurfaceTransitionPolicy.Resolve(
            ImageManagerSurfaceTransitionKind.Closed,
            overlayVisible: true);
        var hiddenDecision = ImageManagerSurfaceTransitionPolicy.Resolve(
            ImageManagerSurfaceTransitionKind.Closed,
            overlayVisible: false);

        visibleDecision.ShouldTouchSurface.Should().BeFalse();
        visibleDecision.RequestZOrderApply.Should().BeTrue();
        visibleDecision.ForceEnforceZOrder.Should().BeFalse();

        hiddenDecision.ShouldTouchSurface.Should().BeFalse();
        hiddenDecision.RequestZOrderApply.Should().BeTrue();
        hiddenDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestApplyWithoutTouch_WhenStateChanged()
    {
        var visibleDecision = ImageManagerSurfaceTransitionPolicy.Resolve(
            ImageManagerSurfaceTransitionKind.StateChanged,
            overlayVisible: true);
        var hiddenDecision = ImageManagerSurfaceTransitionPolicy.Resolve(
            ImageManagerSurfaceTransitionKind.StateChanged,
            overlayVisible: false);

        visibleDecision.ShouldTouchSurface.Should().BeFalse();
        visibleDecision.RequestZOrderApply.Should().BeTrue();
        visibleDecision.ForceEnforceZOrder.Should().BeFalse();

        hiddenDecision.ShouldTouchSurface.Should().BeFalse();
        hiddenDecision.RequestZOrderApply.Should().BeFalse();
        hiddenDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldFallbackToNoTouch_ForUnknownKind()
    {
        var decision = ImageManagerSurfaceTransitionPolicy.Resolve(
            (ImageManagerSurfaceTransitionKind)999,
            overlayVisible: true);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}
