using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherTopmostVisibilityHoldPolicyTests
{
    [Fact]
    public void ResolveVisibleForRepair_ShouldReturnTrue_WhenCurrentlyVisible()
    {
        var nowUtc = DateTime.UtcNow;

        var visible = LauncherWindowPolicies.ResolveVisibleForRepair(
            currentVisibleForTopmost: true,
            lastVisibleForTopmostUtc: DateTime.MinValue,
            nowUtc: nowUtc);

        visible.Should().BeTrue();
    }

    [Fact]
    public void ResolveVisibleForRepair_ShouldReturnFalse_WhenNeverVisible()
    {
        var nowUtc = DateTime.UtcNow;

        var visible = LauncherWindowPolicies.ResolveVisibleForRepair(
            currentVisibleForTopmost: false,
            lastVisibleForTopmostUtc: DateTime.MinValue,
            nowUtc: nowUtc);

        visible.Should().BeFalse();
    }

    [Fact]
    public void ResolveVisibleForRepair_ShouldReturnTrue_WithinHoldWindow()
    {
        var nowUtc = DateTime.UtcNow;

        var visible = LauncherWindowPolicies.ResolveVisibleForRepair(
            currentVisibleForTopmost: false,
            lastVisibleForTopmostUtc: nowUtc.AddMilliseconds(-120),
            nowUtc: nowUtc,
            holdMs: 180);

        visible.Should().BeTrue();
    }

    [Fact]
    public void ResolveVisibleForRepair_ShouldReturnFalse_AfterHoldWindow()
    {
        var nowUtc = DateTime.UtcNow;

        var visible = LauncherWindowPolicies.ResolveVisibleForRepair(
            currentVisibleForTopmost: false,
            lastVisibleForTopmostUtc: nowUtc.AddMilliseconds(-260),
            nowUtc: nowUtc,
            holdMs: 180);

        visible.Should().BeFalse();
    }
}

public sealed class LauncherVisibilityPolicyTests
{
    [Fact]
    public void ResolveForTopmost_ShouldUseMainWindow_WhenNotMinimizedMode()
    {
        var decision = LauncherWindowPolicies.ResolveForTopmost(
            launcherMinimized: false,
            mainVisible: true,
            mainMinimized: false,
            bubbleVisible: true,
            bubbleMinimized: false);

        decision.IsVisible.Should().BeTrue();
        decision.Reason.Should().Be(LauncherTopmostVisibilityReason.MainVisible);
    }

    [Fact]
    public void ResolveForTopmost_ShouldUseBubble_WhenMinimizedMode()
    {
        var decision = LauncherWindowPolicies.ResolveForTopmost(
            launcherMinimized: true,
            mainVisible: true,
            mainMinimized: false,
            bubbleVisible: true,
            bubbleMinimized: false);

        decision.IsVisible.Should().BeTrue();
        decision.Reason.Should().Be(LauncherTopmostVisibilityReason.BubbleVisible);
    }

    [Fact]
    public void ResolveForTopmost_ShouldReturnFalse_WhenSelectedWindowIsMinimized()
    {
        var mainMode = LauncherWindowPolicies.ResolveForTopmost(
            launcherMinimized: false,
            mainVisible: true,
            mainMinimized: true,
            bubbleVisible: true,
            bubbleMinimized: false);

        var bubbleMode = LauncherWindowPolicies.ResolveForTopmost(
            launcherMinimized: true,
            mainVisible: true,
            mainMinimized: false,
            bubbleVisible: true,
            bubbleMinimized: true);

        mainMode.IsVisible.Should().BeFalse();
        mainMode.Reason.Should().Be(LauncherTopmostVisibilityReason.MainHiddenOrMinimized);
        bubbleMode.IsVisible.Should().BeFalse();
        bubbleMode.Reason.Should().Be(LauncherTopmostVisibilityReason.BubbleHiddenOrMinimized);
    }

    [Fact]
    public void IsVisibleForTopmost_ShouldMapResolveDecision()
    {
        LauncherWindowPolicies.IsVisibleForTopmost(
            launcherMinimized: false,
            mainVisible: true,
            mainMinimized: false,
            bubbleVisible: false,
            bubbleMinimized: false).Should().BeTrue();
    }
}

public sealed class LauncherVisibilityTransitionPolicyTests
{
    [Fact]
    public void ResolveMinimize_ContextOverload_ShouldMapToCoreDecision()
    {
        var decision = LauncherWindowPolicies.ResolveMinimizeDecision(
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
        var decision = LauncherWindowPolicies.ResolveRestoreDecision(
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
        var decision = LauncherWindowPolicies.ResolveMinimizeDecision(
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
        var decision = LauncherWindowPolicies.ResolveRestoreDecision(
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
        var decision = LauncherWindowPolicies.ResolveRestoreDecision(
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
        var decision = LauncherWindowPolicies.ResolveMinimizeDecision(
            mainVisible: false,
            bubbleVisible: true);
        var plan = decision.Plan;

        plan.HideMainWindow.Should().BeFalse();
        plan.ShowBubbleWindow.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeFalse();
        decision.Reason.Should().Be(LauncherVisibilityMinimizeReason.NoOp);
    }
}

public sealed class LauncherWindowResolverPolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepBubble_WhenPreferredBubbleAndBubbleVisible()
    {
        var kind = LauncherWindowPolicies.ResolveResolver(
            preferredKind: LauncherWindowKind.Bubble,
            bubbleExists: true,
            bubbleVisible: true,
            mainVisible: false);

        kind.Should().Be(LauncherWindowKind.Bubble);
    }

    [Fact]
    public void Resolve_ShouldFallbackToMain_WhenPreferredBubbleButBubbleNotVisibleAndMainVisible()
    {
        var kind = LauncherWindowPolicies.ResolveResolver(
            preferredKind: LauncherWindowKind.Bubble,
            bubbleExists: true,
            bubbleVisible: false,
            mainVisible: true);

        kind.Should().Be(LauncherWindowKind.Main);
    }

    [Fact]
    public void Resolve_ShouldFallbackToVisibleBubble_WhenMainNotVisible()
    {
        var kind = LauncherWindowPolicies.ResolveResolver(
            preferredKind: LauncherWindowKind.Main,
            bubbleExists: true,
            bubbleVisible: true,
            mainVisible: false);

        kind.Should().Be(LauncherWindowKind.Bubble);
    }

    [Fact]
    public void Resolve_ShouldStayMain_WhenBothHiddenAndBubbleMissing()
    {
        var kind = LauncherWindowPolicies.ResolveResolver(
            preferredKind: LauncherWindowKind.Bubble,
            bubbleExists: false,
            bubbleVisible: false,
            mainVisible: false);

        kind.Should().Be(LauncherWindowKind.Main);
    }
}

public class LauncherWindowRuntimeSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldUseBubbleState_WhenLauncherIsMinimized()
    {
        var snapshot = LauncherWindowPolicies.ResolveRuntimeSnapshot(
            launcherMinimized: true,
            mainVisible: false,
            mainMinimized: true,
            mainActive: false,
            bubbleVisible: true,
            bubbleMinimized: false,
            bubbleActive: true);

        snapshot.VisibleForTopmost.Should().BeTrue();
        snapshot.Active.Should().BeTrue();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Bubble);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.PreferBubbleVisible);
    }

    [Fact]
    public void Resolve_ShouldFallbackToMain_WhenLauncherMarkedMinimizedButBubbleNotVisible()
    {
        var snapshot = LauncherWindowPolicies.ResolveRuntimeSnapshot(
            launcherMinimized: true,
            mainVisible: true,
            mainMinimized: false,
            mainActive: true,
            bubbleVisible: false,
            bubbleMinimized: false,
            bubbleActive: false);

        snapshot.VisibleForTopmost.Should().BeTrue();
        snapshot.Active.Should().BeTrue();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Main);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible);
    }

    [Fact]
    public void Resolve_ShouldUseMainWindowState_WhenLauncherIsNotMinimized()
    {
        var snapshot = LauncherWindowPolicies.ResolveRuntimeSnapshot(
            launcherMinimized: false,
            mainVisible: true,
            mainMinimized: false,
            mainActive: true,
            bubbleVisible: false,
            bubbleMinimized: false,
            bubbleActive: false);

        snapshot.VisibleForTopmost.Should().BeTrue();
        snapshot.Active.Should().BeTrue();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Main);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.PreferMainVisible);
    }

    [Fact]
    public void Resolve_ShouldFallbackToBubble_WhenMainTemporarilyHiddenAndBubbleVisible()
    {
        var snapshot = LauncherWindowPolicies.ResolveRuntimeSnapshot(
            launcherMinimized: false,
            mainVisible: false,
            mainMinimized: true,
            mainActive: false,
            bubbleVisible: true,
            bubbleMinimized: false,
            bubbleActive: true);

        snapshot.VisibleForTopmost.Should().BeTrue();
        snapshot.Active.Should().BeTrue();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Bubble);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible);
    }

    [Fact]
    public void Resolve_ShouldTreatMinimizedBubbleAsNotVisible()
    {
        var snapshot = LauncherWindowPolicies.ResolveRuntimeSnapshot(
            launcherMinimized: true,
            mainVisible: false,
            mainMinimized: true,
            mainActive: false,
            bubbleVisible: true,
            bubbleMinimized: true,
            bubbleActive: true);

        snapshot.VisibleForTopmost.Should().BeFalse();
        snapshot.Active.Should().BeFalse();
        snapshot.WindowKind.Should().Be(LauncherWindowKind.Bubble);
        snapshot.SelectionReason.Should().Be(LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible);
    }
}
