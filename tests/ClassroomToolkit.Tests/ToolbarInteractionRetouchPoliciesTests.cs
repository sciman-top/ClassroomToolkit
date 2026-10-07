using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using System;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionActivationSuppressionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNonActivatedTrigger_ForNonActivatedTrigger()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionActivationSuppression(
            trigger: ToolbarInteractionRetouchTrigger.PreviewMouseDown,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc,
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.NonActivatedTrigger);
    }

    [Fact]
    public void Resolve_ShouldReturnPreviewTimestampUnset_WhenPreviewTimestampUnset()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionActivationSuppression(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: WindowDedupDefaults.UnsetTimestampUtc,
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.PreviewTimestampUnset);
    }

    [Fact]
    public void Resolve_ShouldNotSuppress_WhenWithinSuppressionWindow_AndLauncherOnlyDrift()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionActivationSuppression(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-40),
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.None);
    }

    [Fact]
    public void Resolve_ShouldReturnOutsideSuppressionWindow_WhenOutsideSuppressionWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionActivationSuppression(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-200),
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.OutsideSuppressionWindow);
    }

    [Fact]
    public void Resolve_ShouldReturnNotSuppressed_WhenToolbarAlsoDrifts()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false, toolbarTopmost: false);

        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionActivationSuppression(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-40),
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.None);
    }

    [Fact]
    public void ShouldSuppress_ShouldMapResolveDecision()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: false);

        ToolbarInteractionRetouchPolicies.ShouldSuppress(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-40),
            lastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90).Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldSuppressActivated_WhenPreviewAlreadyRetouchedInWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: true, toolbarTopmost: true);

        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionActivationSuppression(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-20),
            lastRetouchUtc: nowUtc.AddMilliseconds(-10),
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.PreviewAlreadyRetouched);
    }

    [Fact]
    public void Resolve_ShouldNotSuppress_WhenRetouchIsEarlierThanPreview()
    {
        var nowUtc = DateTime.UtcNow;
        var snapshot = CreateSnapshot(launcherTopmost: true, toolbarTopmost: true);

        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionActivationSuppression(
            trigger: ToolbarInteractionRetouchTrigger.Activated,
            snapshot: snapshot,
            lastPreviewMouseDownUtc: nowUtc.AddMilliseconds(-10),
            lastRetouchUtc: nowUtc.AddMilliseconds(-20),
            nowUtc: nowUtc,
            launcherOnlySuppressionMs: 90);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionActivationSuppressionReason.None);
    }

    private static ToolbarInteractionRetouchSnapshot CreateSnapshot(
        bool launcherTopmost,
        bool toolbarTopmost = true)
    {
        return new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: toolbarTopmost,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: launcherTopmost);
    }
}

public sealed class ToolbarInteractionDirectRepairAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenZOrderApplying()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionDirectRepairAdmission(
            zOrderApplying: true,
            zOrderQueued: false);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionDirectRepairAdmissionReason.ZOrderApplying);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenZOrderQueued()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionDirectRepairAdmission(
            zOrderApplying: false,
            zOrderQueued: true);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionDirectRepairAdmissionReason.ZOrderQueued);
    }

    [Fact]
    public void Resolve_ShouldAllow_WhenNoZOrderPressure()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveToolbarInteractionDirectRepairAdmission(
            zOrderApplying: false,
            zOrderQueued: false);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionDirectRepairAdmissionReason.None);
    }
}

public sealed class ToolbarInteractionRetouchDecisionPolicyTests
{
    [Fact]
    public void Resolve_ShouldDisableRetouch_WhenOverlayIsHidden()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveDecision(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: false,
                PhotoModeActive: true,
                WhiteboardActive: true,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: true),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.SceneNotInteractive);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Resolve_ShouldEnableRetouchWithoutForce_WhenInteractiveSceneIsActive(
        bool photoModeActive,
        bool whiteboardActive)
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveDecision(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: photoModeActive,
                WhiteboardActive: whiteboardActive,
                ToolbarVisible: true,
                ToolbarTopmost: false,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: true),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.None);
    }

    [Fact]
    public void Resolve_ShouldNotForce_WhenNoInteractiveSceneIsActive()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveDecision(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: false,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: false,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.SceneNotInteractive);
    }

    [Fact]
    public void Resolve_ShouldForceRetouch_OnPreviewMouseDown_WhenLauncherVisibleInInteractiveScene()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveDecision(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: true,
                ToolbarVisible: true,
                ToolbarTopmost: false,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.PreviewMouseDown);

        decision.ShouldRetouch.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.None);
    }

    [Fact]
    public void Resolve_ShouldSkipRetouch_OnPreviewMouseDown_WhenLauncherNotVisible()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveDecision(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: true,
                RollCallTopmost: true,
                LauncherVisible: false,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.PreviewMouseDown);

        decision.ShouldRetouch.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.PreviewMouseDown);
    }

    [Fact]
    public void Resolve_ShouldForceRetouch_WhenNoTopmostDriftButLauncherVisible()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveDecision(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: true,
                RollCallTopmost: true,
                LauncherVisible: true,
                LauncherTopmost: true),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.None);
    }

    [Fact]
    public void Resolve_ShouldSkipRetouch_WhenNoTopmostDriftAndLauncherNotVisible()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveDecision(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: true,
                RollCallTopmost: true,
                LauncherVisible: false,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.NoTopmostDrift);
    }

    [Fact]
    public void Resolve_ShouldRetouchWithoutForce_WhenLauncherLosesTopmost()
    {
        var decision = ToolbarInteractionRetouchPolicies.ResolveDecision(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.None);
    }
}

public sealed class ToolbarInteractionRetouchDispatchPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnImmediate_WhenActivatedDirectRepairInInteractiveScene_AndLauncherDrifts()
    {
        var mode = ToolbarInteractionRetouchPolicies.ResolveDispatch(
            ToolbarInteractionRetouchTrigger.Activated,
            CreateSnapshot(photoModeActive: true, whiteboardActive: false),
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        mode.Should().Be(ToolbarInteractionRetouchDispatchMode.Immediate);
    }

    [Fact]
    public void Resolve_ShouldReturnBackground_WhenActivatedDirectRepairInInteractiveScene_WithoutLauncherDrift()
    {
        var mode = ToolbarInteractionRetouchPolicies.ResolveDispatch(
            ToolbarInteractionRetouchTrigger.Activated,
            CreateSnapshot(photoModeActive: true, whiteboardActive: false, launcherTopmost: true),
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        mode.Should().Be(ToolbarInteractionRetouchDispatchMode.Background);
    }

    [Fact]
    public void Resolve_ShouldReturnImmediate_WhenNonInteractiveScene()
    {
        var mode = ToolbarInteractionRetouchPolicies.ResolveDispatch(
            ToolbarInteractionRetouchTrigger.Activated,
            CreateSnapshot(photoModeActive: false, whiteboardActive: false),
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        mode.Should().Be(ToolbarInteractionRetouchDispatchMode.Immediate);
    }

    [Fact]
    public void Resolve_ShouldReturnImmediate_WhenExecutionIsNotDirectRepair()
    {
        var mode = ToolbarInteractionRetouchPolicies.ResolveDispatch(
            ToolbarInteractionRetouchTrigger.Activated,
            CreateSnapshot(photoModeActive: true, whiteboardActive: false),
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false));

        mode.Should().Be(ToolbarInteractionRetouchDispatchMode.Immediate);
    }

    private static ToolbarInteractionRetouchSnapshot CreateSnapshot(bool photoModeActive, bool whiteboardActive, bool launcherTopmost = false)
    {
        return new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: photoModeActive,
            WhiteboardActive: whiteboardActive,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: launcherTopmost);
    }
}

public sealed class ToolbarInteractionRetouchExecutionPlanPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, true, false, true, true)]
    public void Resolve_ShouldMapDecisionToOneExecutionPath(
        bool shouldRetouch,
        bool forceEnforce,
        bool applyDirectRepair,
        bool requestZOrderApply,
        bool forceZOrderApply)
    {
        var plan = ToolbarInteractionRetouchPolicies.ResolveExecutionPlan(
            new ToolbarInteractionRetouchDecision(
                shouldRetouch,
                forceEnforce,
                ToolbarInteractionRetouchDecisionReason.None));

        plan.ApplyDirectDriftRepair.Should().Be(applyDirectRepair);
        plan.RequestZOrderApply.Should().Be(requestZOrderApply);
        plan.ForceEnforceZOrder.Should().Be(forceZOrderApply);
    }
}

public sealed class ToolbarInteractionRetouchIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldReturnInteractiveInterval_WhenPhotoModeActive()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: true);

        var value = ToolbarInteractionRetouchPolicies.ResolveMs(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        value.Should().Be(220);
    }

    [Fact]
    public void ResolveMs_ShouldReturnInteractiveInterval_WhenWhiteboardActive()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: false,
            WhiteboardActive: true,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: true);

        var value = ToolbarInteractionRetouchPolicies.ResolveMs(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        value.Should().Be(220);
    }

    [Fact]
    public void ResolveMs_ShouldReturnDefault_WhenSceneNotInteractive()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: false,
            PhotoModeActive: true,
            WhiteboardActive: true,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: true);

        var value = ToolbarInteractionRetouchPolicies.ResolveMs(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        value.Should().Be(120);
    }
}
