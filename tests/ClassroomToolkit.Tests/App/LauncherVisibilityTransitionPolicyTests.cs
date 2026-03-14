using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherVisibilityTransitionPolicyTests
{
    [Fact]
    public void ResolveMinimize_ContextOverload_ShouldMapToCoreDecision()
    {
        var decision = LauncherVisibilityTransitionPolicy.ResolveMinimizeDecision(
            new LauncherMinimizeTransitionContext(
                MainVisible: true,
                BubbleVisible: false));
        var plan = decision.Plan;

        plan.HideMainWindow.Should().BeTrue();
        plan.ShowBubbleWindow.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        decision.Reason.Should().Be(LauncherVisibilityMinimizeReason.HideMainAndShowBubble);
    }

    [Fact]
    public void ResolveRestore_ContextOverload_ShouldMapToCoreDecision()
    {
        var decision = LauncherVisibilityTransitionPolicy.ResolveRestoreDecision(
            new LauncherRestoreTransitionContext(
                MainVisible: false,
                MainActive: false,
                BubbleVisible: true));
        var plan = decision.Plan;

        plan.ShowMainWindow.Should().BeTrue();
        plan.HideBubbleWindow.Should().BeTrue();
        plan.ActivateMainWindow.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        decision.Reason.Should().Be(LauncherVisibilityRestoreReason.ShowMainAndHideBubble);
    }

    [Fact]
    public void ResolveMinimize_ShouldHideMain_ShowBubble_AndRequestZOrder()
    {
        var decision = LauncherVisibilityTransitionPolicy.ResolveMinimizeDecision(
            mainVisible: true,
            bubbleVisible: false);
        var plan = decision.Plan;

        plan.HideMainWindow.Should().BeTrue();
        plan.ShowBubbleWindow.Should().BeTrue();
        plan.ActivateMainWindow.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(LauncherVisibilityMinimizeReason.HideMainAndShowBubble);
    }

    [Fact]
    public void ResolveRestore_ShouldHideBubble_ShowMain_ActivateAndRequestZOrder_WhenMainInactive()
    {
        var decision = LauncherVisibilityTransitionPolicy.ResolveRestoreDecision(
            mainVisible: false,
            mainActive: false,
            bubbleVisible: true);
        var plan = decision.Plan;

        plan.ShowMainWindow.Should().BeTrue();
        plan.HideBubbleWindow.Should().BeTrue();
        plan.ActivateMainWindow.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(LauncherVisibilityRestoreReason.ShowMainAndHideBubble);
    }

    [Fact]
    public void ResolveRestore_ShouldSkipActivation_WhenMainAlreadyActive()
    {
        var decision = LauncherVisibilityTransitionPolicy.ResolveRestoreDecision(
            mainVisible: true,
            mainActive: true,
            bubbleVisible: false);
        var plan = decision.Plan;

        plan.ShowMainWindow.Should().BeFalse();
        plan.HideBubbleWindow.Should().BeFalse();
        plan.ActivateMainWindow.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(LauncherVisibilityRestoreReason.NoOp);
    }

    [Fact]
    public void ResolveMinimize_ShouldSkipZOrder_WhenAlreadyMinimizedState()
    {
        var decision = LauncherVisibilityTransitionPolicy.ResolveMinimizeDecision(
            mainVisible: false,
            bubbleVisible: true);
        var plan = decision.Plan;

        plan.HideMainWindow.Should().BeFalse();
        plan.ShowBubbleWindow.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherVisibilityMinimizeReason.NoOp);
    }
}
