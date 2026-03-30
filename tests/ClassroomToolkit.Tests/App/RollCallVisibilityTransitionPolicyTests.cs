using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallVisibilityTransitionPolicyTests
{
    [Fact]
    public void Resolve_ContextOverload_ShouldMapToCoreDecision()
    {
        var plan = RollCallVisibilityTransitionPolicy.Resolve(
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
        var plan = RollCallVisibilityTransitionPolicy.Resolve(
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
        var plan = RollCallVisibilityTransitionPolicy.Resolve(
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
        var plan = RollCallVisibilityTransitionPolicy.Resolve(
            rollCallVisible: false,
            rollCallActive: true,
            overlayVisible: false);

        plan.SyncOwnerToOverlay.Should().BeFalse();
        plan.ShowWindow.Should().BeTrue();
        plan.ActivateWindow.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }
}
