using AwesomeAssertions;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using System;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingTopmostApplyPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnforce_WhenNoLastState()
    {
        var current = new FloatingTopmostPlan(true, true, true, false, false);

        var decision = FloatingTopmostPolicies.ResolveApply(
            lastFrontSurface: null,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: null,
            currentPlan: current);

        decision.ShouldEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostPolicies.FloatingTopmostApplyReason.MissingLastState);
    }

    [Fact]
    public void Resolve_ShouldEnforce_WhenFrontSurfaceChanged()
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, false);

        var decision = FloatingTopmostPolicies.ResolveApply(
            lastFrontSurface: ZOrderSurface.Whiteboard,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: plan,
            currentPlan: plan);

        decision.ShouldEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostPolicies.FloatingTopmostApplyReason.FrontSurfaceChanged);
    }

    [Fact]
    public void Resolve_ShouldNotEnforce_WhenFrontSurfaceAndPlanUnchanged()
    {
        var plan = new FloatingTopmostPlan(true, true, false, false, false);

        var decision = FloatingTopmostPolicies.ResolveApply(
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: plan,
            currentPlan: plan);

        decision.ShouldEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostPolicies.FloatingTopmostApplyReason.Unchanged);
    }

    [Fact]
    public void Resolve_ShouldNotEnforce_WhenOnlyOverlayActivationIntentChanged()
    {
        var lastPlan = new FloatingTopmostPlan(true, true, false, false, false);
        var currentPlan = new FloatingTopmostPlan(true, true, false, false, true);

        var decision = FloatingTopmostPolicies.ResolveApply(
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: lastPlan,
            currentPlan: currentPlan);

        decision.ShouldEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostPolicies.FloatingTopmostApplyReason.Unchanged);
    }

    [Theory]
    [InlineData(ZOrderSurface.PresentationFullscreen)]
    [InlineData(ZOrderSurface.Whiteboard)]
    public void Resolve_ShouldEnforce_WhenLauncherVisibleOnRetouchSurfaceAndPlanUnchanged(ZOrderSurface surface)
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, true);

        var decision = FloatingTopmostPolicies.ResolveApply(
            lastFrontSurface: surface,
            currentFrontSurface: surface,
            lastPlan: plan,
            currentPlan: plan);

        decision.ShouldEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostPolicies.FloatingTopmostApplyReason.LauncherInteractiveRetouch);
    }

    [Fact]
    public void Resolve_ShouldNotEnforce_WhenLauncherVisibleOnPhotoSurfaceAndPlanUnchanged()
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, true);

        var decision = FloatingTopmostPolicies.ResolveApply(
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: plan,
            currentPlan: plan);

        decision.ShouldEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostPolicies.FloatingTopmostApplyReason.Unchanged);
    }

    [Fact]
    public void Resolve_ShouldEnforce_WhenForceRequested()
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, false);

        var decision = FloatingTopmostPolicies.ResolveApply(
            lastFrontSurface: ZOrderSurface.PhotoFullscreen,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: plan,
            currentPlan: plan,
            forceEnforceZOrder: true);

        decision.ShouldEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostPolicies.FloatingTopmostApplyReason.ForceRequested);
    }

    [Fact]
    public void ShouldEnforceZOrder_ShouldMapResolveDecision()
    {
        var plan = new FloatingTopmostPlan(true, true, true, false, false);

        FloatingTopmostPolicies.ShouldEnforceZOrder(
            lastFrontSurface: null,
            currentFrontSurface: ZOrderSurface.PhotoFullscreen,
            lastPlan: null,
            currentPlan: plan).Should().BeTrue();
    }
}

public sealed class FloatingTopmostDriftPolicyTests
{
    [Fact]
    public void ResolveDrift_ShouldReturnLauncherDrift_WhenLauncherVisibleButNotTopmost()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: false);

        var decision = FloatingTopmostPolicies.ResolveDrift(snapshot);
        decision.HasDrift.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostDriftReason.LauncherDrift);
    }

    [Fact]
    public void ResolveDrift_ShouldReturnNoDrift_WhenAllVisibleWindowsAreTopmost()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: true,
            RollCallTopmost: true,
            LauncherVisible: true,
            LauncherTopmost: true);

        var decision = FloatingTopmostPolicies.ResolveDrift(snapshot);
        decision.HasDrift.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostDriftReason.NoDrift);
    }

    [Fact]
    public void ResolveForceEnforce_ShouldReturnDisabledByDesign_WhenLauncherNotTopmost()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: false);

        var decision = FloatingTopmostPolicies.ResolveForceEnforce(snapshot);
        decision.ShouldForceEnforce.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostForceEnforceReason.DisabledByDesign);
    }

    [Fact]
    public void ResolveForceEnforce_ShouldReturnDisabledByDesign_WhenLauncherHidden()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: false,
            LauncherTopmost: false);

        var decision = FloatingTopmostPolicies.ResolveForceEnforce(snapshot);
        decision.ShouldForceEnforce.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostForceEnforceReason.DisabledByDesign);
    }

    [Fact]
    public void HasDrift_ShouldMapResolveDecision()
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

        FloatingTopmostPolicies.HasDrift(snapshot).Should().BeTrue();
    }
}

public sealed class FloatingTopmostDriftRepairEnforcePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_ForActivatedTrigger_WhenLauncherDriftsInInteractiveScene()
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
            LauncherTopmost: false);

        var enforce = FloatingTopmostPolicies.ResolveDriftRepairEnforce(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        enforce.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_ForPreviewMouseDownTrigger()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: false,
            WhiteboardActive: true,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: true,
            RollCallTopmost: false,
            LauncherVisible: false,
            LauncherTopmost: false);

        var enforce = FloatingTopmostPolicies.ResolveDriftRepairEnforce(
            snapshot,
            ToolbarInteractionRetouchTrigger.PreviewMouseDown);

        enforce.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_ForActivatedTrigger_WhenLauncherDoesNotDrift()
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

        var enforce = FloatingTopmostPolicies.ResolveDriftRepairEnforce(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        enforce.Should().BeFalse();
    }
}

public sealed class FloatingTopmostDriftRepairPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNoRepairs_WhenAllVisibleWindowsAreTopmost()
    {
        var plan = FloatingTopmostPolicies.ResolveDriftRepair(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: true,
                RollCallTopmost: true,
                LauncherVisible: true,
                LauncherTopmost: true));

        plan.RepairToolbar.Should().BeFalse();
        plan.RepairRollCall.Should().BeFalse();
        plan.RepairLauncher.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRepairOnlyDriftedVisibleWindows()
    {
        var plan = FloatingTopmostPolicies.ResolveDriftRepair(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: false,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: false));

        plan.RepairToolbar.Should().BeTrue();
        plan.RepairRollCall.Should().BeFalse();
        plan.RepairLauncher.Should().BeTrue();
    }
}

public sealed class FloatingTopmostPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableImageManagerTopmost_WhenImageManagerIsFront()
    {
        var plan = FloatingTopmostPolicies.ResolvePlan(
            frontSurface: ZOrderSurface.ImageManager,
            toolbarVisible: true,
            rollCallVisible: true,
            launcherVisible: true,
            imageManagerVisible: true,
            overlayVisible: true);

        plan.ImageManagerTopmost.Should().BeTrue();
        plan.OverlayShouldActivate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestOverlayActivation_WhenPhotoSurfaceHasNoVisibleFloatingUtility()
    {
        var plan = FloatingTopmostPolicies.ResolvePlan(
            frontSurface: ZOrderSurface.PhotoFullscreen,
            toolbarVisible: false,
            rollCallVisible: false,
            launcherVisible: false,
            imageManagerVisible: false,
            overlayVisible: true);

        plan.OverlayShouldActivate.Should().BeTrue();
        plan.ImageManagerTopmost.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldNotRequestOverlayActivation_WhenPhotoSurfaceHasVisibleFloatingUtility()
    {
        var plan = FloatingTopmostPolicies.ResolvePlan(
            frontSurface: ZOrderSurface.PhotoFullscreen,
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: true,
            imageManagerVisible: false,
            overlayVisible: true);

        plan.OverlayShouldActivate.Should().BeFalse();
        plan.ImageManagerTopmost.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestOverlayActivation_WhenWhiteboardIsFront()
    {
        var plan = FloatingTopmostPolicies.ResolvePlan(
            frontSurface: ZOrderSurface.Whiteboard,
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: true,
            imageManagerVisible: true,
            overlayVisible: true);

        plan.OverlayShouldActivate.Should().BeTrue();
        plan.ImageManagerTopmost.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableTopmostFlags_WhenWindowsAreHidden()
    {
        var plan = FloatingTopmostPolicies.ResolvePlan(
            frontSurface: ZOrderSurface.None,
            toolbarVisible: false,
            rollCallVisible: false,
            launcherVisible: false,
            imageManagerVisible: false,
            overlayVisible: false);

        plan.ToolbarTopmost.Should().BeFalse();
        plan.RollCallTopmost.Should().BeFalse();
        plan.LauncherTopmost.Should().BeFalse();
        plan.ImageManagerTopmost.Should().BeFalse();
        plan.OverlayShouldActivate.Should().BeFalse();
    }

    [Theory]
    [InlineData(ZOrderSurface.PhotoFullscreen)]
    [InlineData(ZOrderSurface.Whiteboard)]
    [InlineData(ZOrderSurface.PresentationFullscreen)]
    [InlineData(ZOrderSurface.ImageManager)]
    public void Resolve_ShouldKeepLauncherTopmost_WhenLauncherVisible(ZOrderSurface surface)
    {
        var plan = FloatingTopmostPolicies.ResolvePlan(
            frontSurface: surface,
            toolbarVisible: true,
            rollCallVisible: true,
            launcherVisible: true,
            imageManagerVisible: true,
            overlayVisible: true);

        plan.LauncherTopmost.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldSupportVisibilitySnapshotInput()
    {
        var visibility = new FloatingTopmostVisibilitySnapshot(
            ToolbarVisible: true,
            RollCallVisible: false,
            LauncherVisible: true,
            ImageManagerVisible: false,
            OverlayVisible: true);

        var plan = FloatingTopmostPolicies.ResolvePlan(
            ZOrderSurface.Whiteboard,
            visibility);

        plan.ToolbarTopmost.Should().BeTrue();
        plan.RollCallTopmost.Should().BeFalse();
        plan.LauncherTopmost.Should().BeTrue();
        plan.ImageManagerTopmost.Should().BeFalse();
        plan.OverlayShouldActivate.Should().BeTrue();
    }
}

public sealed class FloatingTopmostRetouchPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_WhenOverlayTopmostTurnsOn()
    {
        var previous = UiSessionState.Default with { OverlayTopmostRequired = false };
        var current = previous with { OverlayTopmostRequired = true };
        var transition = new UiSessionTransition(1, DateTime.UtcNow, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image), previous, current);

        var decision = FloatingTopmostPolicies.ResolveRetouch(transition);
        decision.ShouldEnsureFloatingOnTransition.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostRetouchReason.OverlayTopmostBecameRequired);
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenTopmostStateUnchanged()
    {
        var previous = UiSessionState.Default with { OverlayTopmostRequired = true };
        var current = previous;
        var transition = new UiSessionTransition(2, DateTime.UtcNow, new SwitchToolModeEvent(UiToolMode.Cursor), previous, current);

        var decision = FloatingTopmostPolicies.ResolveRetouch(transition);
        decision.ShouldEnsureFloatingOnTransition.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostRetouchReason.OverlayTopmostNotRising);
    }

    [Fact]
    public void ShouldEnsureFloatingOnTransition_ShouldMapResolveDecision()
    {
        var previous = UiSessionState.Default with { OverlayTopmostRequired = false };
        var current = previous with { OverlayTopmostRequired = true };
        var transition = new UiSessionTransition(3, DateTime.UtcNow, new EnterWhiteboardEvent(), previous, current);

        FloatingTopmostPolicies.ShouldEnsureFloatingOnTransition(transition).Should().BeTrue();
    }
}

public sealed class FloatingTopmostWatchdogPolicyTests
{
    [Fact]
    public void ShouldForceRetouch_ShouldReturnFalse_WhenPhotoModeActive()
    {
        var shouldRetouch = FloatingTopmostPolicies.ShouldForceRetouch(
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: false,
            imageManagerVisible: false,
            rollCallAuxOverlayVisible: false,
            photoModeActive: true);

        shouldRetouch.Should().BeFalse();
    }

    [Fact]
    public void ShouldForceRetouch_ShouldReturnTrue_WhenPhotoModeInactiveAndAnyUtilityVisible()
    {
        var shouldRetouch = FloatingTopmostPolicies.ShouldForceRetouch(
            toolbarVisible: false,
            rollCallVisible: true,
            launcherVisible: false,
            imageManagerVisible: false,
            rollCallAuxOverlayVisible: false,
            photoModeActive: false);

        shouldRetouch.Should().BeTrue();
    }
}
