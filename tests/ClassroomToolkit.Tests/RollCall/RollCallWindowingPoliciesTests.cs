using AwesomeAssertions;
using ClassroomToolkit.App.RollCall;
using ClassroomToolkit.Interop;
using Xunit;

namespace ClassroomToolkit.Tests.RollCall;

public sealed class RollCallAuxOverlayTopmostPolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepPhotoOverlayInTopmostBand_WhenVisible()
    {
        var plan = RollCallWindowingPolicies.ResolveRollCallAuxOverlayTopmost(
            photoOverlayVisible: true,
            groupOverlayVisible: true,
            enforceZOrder: true);

        plan.PhotoOverlayTopmost.Should().BeTrue();
        plan.PhotoOverlayEnforceZOrder.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Resolve_ShouldKeepGroupOverlayEnforceBehavior(bool enforceZOrder)
    {
        var plan = RollCallWindowingPolicies.ResolveRollCallAuxOverlayTopmost(
            photoOverlayVisible: false,
            groupOverlayVisible: true,
            enforceZOrder: enforceZOrder);

        plan.GroupOverlayTopmost.Should().BeTrue();
        plan.GroupOverlayEnforceZOrder.Should().Be(enforceZOrder);
    }
}

public sealed class RollCallTransparencyPolicyTests
{
    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    public void ResolveTransparency_ShouldFollowHoverAndPaintMode(
        bool hovering,
        bool paintAllowsTransparency,
        bool expected)
    {
        RollCallWindowingPolicies
            .ShouldEnableTransparent(hovering, paintAllowsTransparency)
            .Should()
            .Be(expected);
    }

    [Fact]
    public void ResolveStyleApply_ShouldReturnFalse_WhenStateUnchanged()
    {
        RollCallWindowingPolicies
            .ShouldApplyStyle(transparentEnabled: true, lastTransparentEnabled: true)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ResolveStyleApply_ShouldReturnTrue_WhenStateChangedOrUnknown()
    {
        RollCallWindowingPolicies
            .ShouldApplyStyle(transparentEnabled: false, lastTransparentEnabled: true)
            .Should()
            .BeTrue();
        RollCallWindowingPolicies
            .ShouldApplyStyle(transparentEnabled: false, lastTransparentEnabled: null)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ResolveStyleMasks_ShouldSetTransparentMask_WhenEnabled()
    {
        var (setMask, clearMask) = RollCallWindowingPolicies.ResolveStyleMasks(transparentEnabled: true);

        setMask.Should().Be(NativeMethods.WsExTransparent);
        clearMask.Should().Be(0);
    }

    [Fact]
    public void ResolveStyleMasks_ShouldClearTransparentMask_WhenDisabled()
    {
        var (setMask, clearMask) = RollCallWindowingPolicies.ResolveStyleMasks(transparentEnabled: false);

        setMask.Should().Be(0);
        clearMask.Should().Be(NativeMethods.WsExTransparent);
    }

    [Fact]
    public void ResolveHoverTimer_ShouldReturnExpectedDecisions()
    {
        RollCallWindowingPolicies.ShouldStartHoverTimer(true, false).Should().BeTrue();
        RollCallWindowingPolicies.ShouldStartHoverTimer(true, true).Should().BeFalse();
        RollCallWindowingPolicies.ShouldStopHoverTimer(false, true).Should().BeTrue();
        RollCallWindowingPolicies.ShouldStopHoverTimer(true, true).Should().BeFalse();
    }

}

public sealed class RollCallVisibilityTransitionPolicyTests
{
    [Fact]
    public void Resolve_ContextOverload_ShouldMapToCoreDecision()
    {
        var plan = RollCallWindowingPolicies.ResolveRollCallVisibilityTransition(
            new RollCallVisibilityTransitionContext(
                RollCallVisible: false,
                RollCallActive: false,
                OverlayVisible: true));

        plan.SyncOwnerToOverlay.Should().BeTrue();
        plan.ShowWindow.Should().BeTrue();
        plan.ActivateWindow.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldHideVisibleRollCall_AndRequestZOrder()
    {
        var plan = RollCallWindowingPolicies.ResolveRollCallVisibilityTransition(
            rollCallVisible: true,
            rollCallActive: true,
            overlayVisible: true);

        plan.HideWindow.Should().BeTrue();
        plan.ShowWindow.Should().BeFalse();
        plan.ActivateWindow.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldShowAndActivateHiddenRollCall_WhenOverlayVisible()
    {
        var plan = RollCallWindowingPolicies.ResolveRollCallVisibilityTransition(
            rollCallVisible: false,
            rollCallActive: false,
            overlayVisible: true);

        plan.SyncOwnerToOverlay.Should().BeTrue();
        plan.ShowWindow.Should().BeTrue();
        plan.ActivateWindow.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldSkipActivation_WhenAlreadyActive()
    {
        var plan = RollCallWindowingPolicies.ResolveRollCallVisibilityTransition(
            rollCallVisible: false,
            rollCallActive: true,
            overlayVisible: false);

        plan.SyncOwnerToOverlay.Should().BeFalse();
        plan.ShowWindow.Should().BeTrue();
        plan.ActivateWindow.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }
}
