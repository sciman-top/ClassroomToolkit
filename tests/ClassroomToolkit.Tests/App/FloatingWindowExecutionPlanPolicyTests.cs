using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingWindowExecutionPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldBuildCombinedExecutionPlan()
    {
        var runtimeSnapshot = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: true,
            OverlayActive: false,
            PhotoActive: true,
            PresentationFullscreen: false,
            WhiteboardActive: false,
            ImageManagerVisible: true,
            LauncherVisible: true);
        var topmostPlan = new FloatingTopmostPlan(
            ToolbarTopmost: true,
            RollCallTopmost: false,
            LauncherTopmost: true,
            ImageManagerTopmost: true,
            OverlayShouldActivate: true);
        var utilityActivity = new FloatingUtilityActivitySnapshot(
            ToolbarActive: false,
            RollCallActive: false,
            ImageManagerActive: false,
            LauncherActive: false);
        var ownerSnapshot = new FloatingOwnerRuntimeSnapshot(
            OverlayVisible: true,
            ToolbarOwnerAlreadyOverlay: false,
            RollCallOwnerAlreadyOverlay: true,
            ImageManagerOwnerAlreadyOverlay: false);

        var plan = FloatingWindowExecutionPlanPolicy.Resolve(
            runtimeSnapshot,
            topmostPlan,
            enforceZOrder: true,
            utilityActivity,
            ownerSnapshot,
            suppressOverlayActivation: false);

        plan.TopmostExecutionPlan.EnforceZOrder.Should().BeTrue();
        plan.ActivationPlan.ActivateOverlay.Should().BeTrue();
        plan.ActivationPlan.ActivateImageManager.Should().BeTrue();
        plan.OwnerPlan.ToolbarAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
        plan.OwnerPlan.RollCallAction.Should().Be(FloatingOwnerBindingAction.None);
    }

    [Fact]
    public void Resolve_ShouldSuppressOverlayActivation_WhenRequested()
    {
        var runtimeSnapshot = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: true,
            OverlayActive: false,
            PhotoActive: true,
            PresentationFullscreen: false,
            WhiteboardActive: false,
            ImageManagerVisible: false,
            LauncherVisible: true);
        var topmostPlan = new FloatingTopmostPlan(
            ToolbarTopmost: true,
            RollCallTopmost: true,
            LauncherTopmost: true,
            ImageManagerTopmost: false,
            OverlayShouldActivate: true);
        var utilityActivity = new FloatingUtilityActivitySnapshot(
            ToolbarActive: false,
            RollCallActive: false,
            ImageManagerActive: false,
            LauncherActive: false);
        var ownerSnapshot = new FloatingOwnerRuntimeSnapshot(
            OverlayVisible: true,
            ToolbarOwnerAlreadyOverlay: true,
            RollCallOwnerAlreadyOverlay: true,
            ImageManagerOwnerAlreadyOverlay: false);

        var plan = FloatingWindowExecutionPlanPolicy.Resolve(
            runtimeSnapshot,
            topmostPlan,
            enforceZOrder: false,
            utilityActivity,
            ownerSnapshot,
            suppressOverlayActivation: true);

        plan.ActivationPlan.ActivateOverlay.Should().BeFalse();
    }
}
