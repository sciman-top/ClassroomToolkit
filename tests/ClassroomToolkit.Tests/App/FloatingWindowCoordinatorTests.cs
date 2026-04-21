using System.Collections.Generic;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingWindowCoordinatorTests
{
    private static readonly IWindowOrchestrator Orchestrator = new WindowOrchestrator();

    [Fact]
    public void Apply_ShouldExecutePlanAndReturnUpdatedState()
    {
        var surfaceStack = new List<ZOrderSurface> { ZOrderSurface.PhotoFullscreen };
        var coordination = new FloatingWindowCoordinationSnapshot(
            Runtime: new FloatingWindowRuntimeSnapshot(
                OverlayVisible: true,
                OverlayActive: false,
                PhotoActive: true,
                PresentationFullscreen: false,
                WhiteboardActive: false,
                ImageManagerVisible: true,
                LauncherVisible: true),
            Launcher: new LauncherWindowRuntimeSnapshot(
                WindowKind: LauncherWindowKind.Main,
                VisibleForTopmost: true,
                Active: false,
                SelectionReason: LauncherWindowRuntimeSelectionReason.PreferMainVisible),
            TopmostVisibility: new FloatingTopmostVisibilitySnapshot(
                ToolbarVisible: true,
                RollCallVisible: true,
                LauncherVisible: true,
                ImageManagerVisible: true,
                OverlayVisible: true),
            UtilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false),
            Owner: new FloatingOwnerRuntimeSnapshot(
                OverlayVisible: true,
                ToolbarOwnerAlreadyOverlay: false,
                RollCallOwnerAlreadyOverlay: true,
                ImageManagerOwnerAlreadyOverlay: false));
        var initialState = new FloatingWindowCoordinationState(
            LastFrontSurface: null,
            LastTopmostPlan: null);
        FloatingWindowExecutionPlan? executedPlan = null;

        var nextState = FloatingWindowCoordinator.Apply(
            Orchestrator,
            surfaceStack,
            coordination,
            initialState,
            forceEnforceZOrder: false,
            suppressOverlayActivation: false,
            executePlan: plan => executedPlan = plan);

        nextState.LastFrontSurface.Should().Be(ZOrderSurface.PhotoFullscreen);
        nextState.LastTopmostPlan.Should().NotBeNull();
        executedPlan.Should().NotBeNull();
        executedPlan!.Value.TopmostExecutionPlan.EnforceZOrder.Should().BeTrue();
        executedPlan.Value.ActivationPlan.ActivateOverlay.Should().BeFalse();
        executedPlan.Value.ActivationPlan.ActivateImageManager.Should().BeFalse();
        executedPlan.Value.OwnerPlan.ToolbarAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
        executedPlan.Value.OwnerPlan.RollCallAction.Should().Be(FloatingOwnerBindingAction.None);
    }

    [Fact]
    public void Apply_ShouldRespectOverlayActivationSuppression()
    {
        var surfaceStack = new List<ZOrderSurface> { ZOrderSurface.Whiteboard };
        var coordination = new FloatingWindowCoordinationSnapshot(
            Runtime: new FloatingWindowRuntimeSnapshot(
                OverlayVisible: true,
                OverlayActive: false,
                PhotoActive: false,
                PresentationFullscreen: false,
                WhiteboardActive: true,
                ImageManagerVisible: false,
                LauncherVisible: true),
            Launcher: new LauncherWindowRuntimeSnapshot(
                WindowKind: LauncherWindowKind.Main,
                VisibleForTopmost: true,
                Active: false,
                SelectionReason: LauncherWindowRuntimeSelectionReason.PreferMainVisible),
            TopmostVisibility: new FloatingTopmostVisibilitySnapshot(
                ToolbarVisible: true,
                RollCallVisible: false,
                LauncherVisible: true,
                ImageManagerVisible: false,
                OverlayVisible: true),
            UtilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false),
            Owner: new FloatingOwnerRuntimeSnapshot(
                OverlayVisible: true,
                ToolbarOwnerAlreadyOverlay: true,
                RollCallOwnerAlreadyOverlay: false,
                ImageManagerOwnerAlreadyOverlay: false));

        FloatingWindowExecutionPlan? executedPlan = null;

        var nextState = FloatingWindowCoordinator.Apply(
            Orchestrator,
            surfaceStack,
            coordination,
            new FloatingWindowCoordinationState(
                LastFrontSurface: ZOrderSurface.Whiteboard,
                LastTopmostPlan: FloatingTopmostPlanPolicy.Resolve(
                    ZOrderSurface.Whiteboard,
                    coordination.TopmostVisibility)),
            forceEnforceZOrder: false,
            suppressOverlayActivation: true,
            executePlan: plan => executedPlan = plan);

        nextState.LastFrontSurface.Should().Be(ZOrderSurface.Whiteboard);
        executedPlan.Should().NotBeNull();
        executedPlan!.Value.ActivationPlan.ActivateOverlay.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldSkipExecutePlan_WhenExecutionIsNoOp()
    {
        var surfaceStack = new List<ZOrderSurface> { ZOrderSurface.Whiteboard };
        var topmostVisibility = new FloatingTopmostVisibilitySnapshot(
            ToolbarVisible: true,
            RollCallVisible: false,
            LauncherVisible: false,
            ImageManagerVisible: false,
            OverlayVisible: true);
        var topmostPlan = FloatingTopmostPlanPolicy.Resolve(ZOrderSurface.Whiteboard, topmostVisibility);
        var coordination = new FloatingWindowCoordinationSnapshot(
            Runtime: new FloatingWindowRuntimeSnapshot(
                OverlayVisible: true,
                OverlayActive: true,
                PhotoActive: false,
                PresentationFullscreen: false,
                WhiteboardActive: true,
                ImageManagerVisible: false,
                LauncherVisible: false),
            Launcher: new LauncherWindowRuntimeSnapshot(
                WindowKind: LauncherWindowKind.Main,
                VisibleForTopmost: false,
                Active: false,
                SelectionReason: LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible),
            TopmostVisibility: topmostVisibility,
            UtilityActivity: new FloatingUtilityActivitySnapshot(
                ToolbarActive: true,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false),
            Owner: new FloatingOwnerRuntimeSnapshot(
                OverlayVisible: true,
                ToolbarOwnerAlreadyOverlay: true,
                RollCallOwnerAlreadyOverlay: true,
                ImageManagerOwnerAlreadyOverlay: true));

        var executeCount = 0;
        var nextState = FloatingWindowCoordinator.Apply(
            Orchestrator,
            surfaceStack,
            coordination,
            new FloatingWindowCoordinationState(
                LastFrontSurface: ZOrderSurface.Whiteboard,
                LastTopmostPlan: topmostPlan),
            forceEnforceZOrder: false,
            suppressOverlayActivation: false,
            executePlan: _ => executeCount++);

        executeCount.Should().Be(0);
        nextState.LastFrontSurface.Should().Be(ZOrderSurface.Whiteboard);
    }
}
