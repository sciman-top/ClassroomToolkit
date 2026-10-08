using AwesomeAssertions;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class SessionFloatingWidgetVisibilityPolicyTests
{
    [Fact]
    public void Resolve_ShouldDetectVisibilityChange_WhenAnyWidgetChanges()
    {
        var previous = UiSessionState.Default with
        {
            RollCallVisible = false,
            LauncherVisible = false,
            ToolbarVisible = false
        };
        var current = previous with
        {
            LauncherVisible = true
        };

        var decision = SessionTransitionPolicies.ResolveSessionFloatingWidgetVisibility(previous, current);

        decision.AnyVisibilityChanged.Should().BeTrue();
        decision.AnyWidgetBecameVisible.Should().BeTrue();
        decision.Reason.Should().Be(SessionFloatingWidgetVisibilityReason.LauncherBecameVisible);
    }

    [Fact]
    public void Resolve_ShouldDetectChangeWithoutBecomeVisible_WhenWidgetHides()
    {
        var previous = UiSessionState.Default with
        {
            RollCallVisible = true,
            LauncherVisible = true,
            ToolbarVisible = true
        };
        var current = previous with
        {
            LauncherVisible = false
        };

        var decision = SessionTransitionPolicies.ResolveSessionFloatingWidgetVisibility(previous, current);

        decision.AnyVisibilityChanged.Should().BeTrue();
        decision.AnyWidgetBecameVisible.Should().BeFalse();
        decision.Reason.Should().Be(SessionFloatingWidgetVisibilityReason.VisibilityChangedButNoWidgetBecameVisible);
    }

    [Fact]
    public void Resolve_ShouldReturnNoChanges_WhenVisibilityStateMatches()
    {
        var state = UiSessionState.Default with
        {
            RollCallVisible = true,
            LauncherVisible = true,
            ToolbarVisible = true
        };

        var decision = SessionTransitionPolicies.ResolveSessionFloatingWidgetVisibility(state, state);

        decision.AnyVisibilityChanged.Should().BeFalse();
        decision.AnyWidgetBecameVisible.Should().BeFalse();
        decision.Reason.Should().Be(SessionFloatingWidgetVisibilityReason.None);
    }
}

public sealed class SessionTransitionApplyGatePolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenDecisionHasNoAction()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        var gate = SessionTransitionPolicies.ResolveApplyGate(decision);
        gate.ShouldApply.Should().BeFalse();
        gate.Reason.Should().Be(SessionTransitionApplyGateReason.NoZOrderAction);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenTouchSurfaceRequired()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        var gate = SessionTransitionPolicies.ResolveApplyGate(decision);
        gate.ShouldApply.Should().BeTrue();
        gate.Reason.Should().Be(SessionTransitionApplyGateReason.TouchSurfaceRequested);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenZOrderApplyRequested()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);

        var gate = SessionTransitionPolicies.ResolveApplyGate(decision);
        gate.ShouldApply.Should().BeTrue();
        gate.Reason.Should().Be(SessionTransitionApplyGateReason.ZOrderApplyRequested);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenForceEnforceRequested()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: false,
            ForceEnforceZOrder: true);

        var gate = SessionTransitionPolicies.ResolveApplyGate(decision);
        gate.ShouldApply.Should().BeTrue();
        gate.Reason.Should().Be(SessionTransitionApplyGateReason.ForceEnforceRequested);
    }

    [Fact]
    public void ShouldApply_ShouldMapResolveDecision()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.Whiteboard,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        SessionTransitionPolicies.ShouldApplyApplyGate(decision).Should().BeTrue();
    }
}

public sealed class SessionTransitionApplyPolicyTests
{
    [Fact]
    public void Resolve_ShouldRequestAndForce_WhenFloatingMustBeEnsured()
    {
        var decision = SessionTransitionPolicies.ResolveApply(
            shouldEnsureFloating: true,
            overlayTopmostRequired: true,
            sceneChanged: false,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: false,
                AnyWidgetBecameVisible: false,
                Reason: SessionFloatingWidgetVisibilityReason.None));

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionApplyReason.EnsureFloatingRequested);
    }

    [Fact]
    public void Resolve_ShouldRequestWithForce_WhenSceneChangedIntoTopmostMode()
    {
        var decision = SessionTransitionPolicies.ResolveApply(
            shouldEnsureFloating: false,
            overlayTopmostRequired: true,
            sceneChanged: true,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: false,
                AnyWidgetBecameVisible: false,
                Reason: SessionFloatingWidgetVisibilityReason.None));

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionApplyReason.SceneChanged);
    }

    [Fact]
    public void Resolve_ShouldRequestWithForce_WhenWidgetBecomesVisible()
    {
        var decision = SessionTransitionPolicies.ResolveApply(
            shouldEnsureFloating: false,
            overlayTopmostRequired: true,
            sceneChanged: false,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: true,
                AnyWidgetBecameVisible: true,
                Reason: SessionFloatingWidgetVisibilityReason.LauncherBecameVisible));

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionApplyReason.WidgetBecameVisible);
    }

    [Fact]
    public void Resolve_ShouldNotRequest_WhenOnlyWidgetBecomesHidden()
    {
        var decision = SessionTransitionPolicies.ResolveApply(
            shouldEnsureFloating: false,
            overlayTopmostRequired: false,
            sceneChanged: false,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: true,
                AnyWidgetBecameVisible: false,
                Reason: SessionFloatingWidgetVisibilityReason.VisibilityChangedButNoWidgetBecameVisible));

        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionApplyReason.WidgetVisibilityChangedButNoWidgetBecameVisible);
    }

    [Fact]
    public void Resolve_ShouldRequestWithoutForce_WhenSceneChangedIntoIdleMode()
    {
        var decision = SessionTransitionPolicies.ResolveApply(
            shouldEnsureFloating: false,
            overlayTopmostRequired: false,
            sceneChanged: true,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: false,
                AnyWidgetBecameVisible: false,
                Reason: SessionFloatingWidgetVisibilityReason.None));

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionApplyReason.SceneChanged);
    }
}

public sealed class SessionTransitionDuplicatePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnAdvanced_WhenCurrentIdIsGreater()
    {
        var decision = SessionTransitionPolicies.ResolveDuplicate(
            lastAppliedTransitionId: 10,
            currentTransitionId: 11);

        decision.ShouldApply.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionDuplicateReason.TransitionAdvanced);
    }

    [Fact]
    public void Resolve_ShouldReturnDuplicate_WhenCurrentIdEqualsLast()
    {
        var decision = SessionTransitionPolicies.ResolveDuplicate(
            lastAppliedTransitionId: 10,
            currentTransitionId: 10);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionDuplicateReason.DuplicateTransitionId);
    }

    [Fact]
    public void Resolve_ShouldReturnRegressed_WhenCurrentIdLessThanLast()
    {
        var decision = SessionTransitionPolicies.ResolveDuplicate(
            lastAppliedTransitionId: 10,
            currentTransitionId: 9);

        decision.ShouldApply.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionDuplicateReason.RegressedTransitionId);
    }

    [Fact]
    public void ShouldApply_ShouldMapResolveDecision()
    {
        SessionTransitionPolicies.ShouldApplyDuplicate(
            lastAppliedTransitionId: 10,
            currentTransitionId: 11).Should().BeTrue();
    }
}

public sealed class SessionTransitionDuplicateResetPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnResetRequired_WhenOverlayRewiredAndLastIdExists()
    {
        var decision = SessionTransitionPolicies.ResolveDuplicateReset(
            overlayWindowRewired: true,
            lastAppliedTransitionId: 12);

        decision.ShouldReset.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionDuplicateResetReason.ResetRequired);
    }

    [Fact]
    public void Resolve_ShouldReturnOverlayNotRewired_WhenOverlayNotRewired()
    {
        var decision = SessionTransitionPolicies.ResolveDuplicateReset(
            overlayWindowRewired: false,
            lastAppliedTransitionId: 12);

        decision.ShouldReset.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionDuplicateResetReason.OverlayNotRewired);
    }

    [Fact]
    public void Resolve_ShouldReturnNoAppliedTransition_WhenNoLastId()
    {
        var decision = SessionTransitionPolicies.ResolveDuplicateReset(
            overlayWindowRewired: true,
            lastAppliedTransitionId: 0);

        decision.ShouldReset.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionDuplicateResetReason.NoAppliedTransition);
    }

    [Fact]
    public void ShouldReset_ShouldMapResolveDecision()
    {
        SessionTransitionPolicies.ShouldReset(
            overlayWindowRewired: true,
            lastAppliedTransitionId: 12).Should().BeTrue();
    }
}

public sealed class SessionTransitionEventAdmissionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenStateUnchanged()
    {
        var decision = SessionTransitionPolicies.ResolveEventAdmission(
            hasStateChange: false,
            lastAppliedTransitionId: 10,
            currentTransitionId: 11);

        decision.ShouldProcess.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionAdmissionReason.NoStateChange);
    }

    [Fact]
    public void Resolve_ShouldReject_WhenTransitionIdNotAdvanced()
    {
        var decision = SessionTransitionPolicies.ResolveEventAdmission(
            hasStateChange: true,
            lastAppliedTransitionId: 10,
            currentTransitionId: 10);

        decision.ShouldProcess.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionAdmissionReason.DuplicateTransitionId);
    }

    [Fact]
    public void Resolve_ShouldRejectWithRegressedReason_WhenTransitionIdRegressed()
    {
        var decision = SessionTransitionPolicies.ResolveEventAdmission(
            hasStateChange: true,
            lastAppliedTransitionId: 10,
            currentTransitionId: 9);

        decision.ShouldProcess.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionAdmissionReason.RegressedTransitionId);
    }

    [Fact]
    public void Resolve_ShouldAccept_WhenStateChangedAndTransitionAdvanced()
    {
        var decision = SessionTransitionPolicies.ResolveEventAdmission(
            hasStateChange: true,
            lastAppliedTransitionId: 10,
            currentTransitionId: 11);

        decision.ShouldProcess.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionAdmissionReason.None);
    }

    [Fact]
    public void ShouldProcess_ShouldMapResolveDecision()
    {
        SessionTransitionPolicies.ShouldProcess(
            hasStateChange: true,
            lastAppliedTransitionId: 10,
            currentTransitionId: 11).Should().BeTrue();
    }
}

public sealed class SessionTransitionSurfacePolicyTests
{
    [Fact]
    public void Resolve_ShouldTouchAndMapSurface_WhenSceneChangesToWhiteboard()
    {
        var previous = UiSessionState.Default;
        var current = previous with { Scene = UiSceneKind.Whiteboard };

        var decision = SessionTransitionPolicies.ResolveSurface(previous, current);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.Whiteboard);
        decision.Reason.Should().Be(SessionTransitionSurfaceReason.SurfaceRetouchRequested);
    }

    [Fact]
    public void Resolve_ShouldReturnNoTouch_WhenSceneUnchanged()
    {
        var previous = UiSessionState.Default with { Scene = UiSceneKind.PhotoFullscreen };
        var current = previous with { InkDirty = true };

        var decision = SessionTransitionPolicies.ResolveSurface(previous, current);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.Reason.Should().Be(SessionTransitionSurfaceReason.NoSurfaceRetouchRequested);
    }
}

public sealed class SessionTransitionWindowingPolicyTests
{
    [Fact]
    public void Resolve_ShouldTouchPhotoSurface_AndRequestApply_WhenSceneChangesToPhoto()
    {
        var transition = new UiSessionTransition(
            1,
            DateTime.UtcNow,
            new EnterPhotoFullscreenEvent(PhotoSourceKind.Image),
            UiSessionState.Default,
            UiSessionState.Default with
            {
                Scene = UiSceneKind.PhotoFullscreen,
                OverlayTopmostRequired = true,
                RollCallVisible = true,
                LauncherVisible = true,
                ToolbarVisible = true
            });

        var decision = SessionTransitionPolicies.ResolveDecision(transition);

        decision.ZOrderDecision.ShouldTouchSurface.Should().BeTrue();
        decision.ZOrderDecision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.ZOrderDecision.RequestZOrderApply.Should().BeTrue();
        decision.ZOrderDecision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionWindowingReason.EnsureFloatingRequested);
        decision.WidgetVisibilityReason.Should().Be(SessionFloatingWidgetVisibilityReason.RollCallBecameVisible);
        decision.ApplyReason.Should().Be(SessionTransitionApplyReason.EnsureFloatingRequested);
        decision.SurfaceReason.Should().Be(SessionTransitionSurfaceReason.SurfaceRetouchRequested);
    }

    [Fact]
    public void Resolve_ShouldRequestApplyWithoutTouch_WhenSceneChangesToIdle()
    {
        var previous = UiSessionState.Default with
        {
            Scene = UiSceneKind.PhotoFullscreen,
            OverlayTopmostRequired = true
        };
        var current = UiSessionState.Default with
        {
            Scene = UiSceneKind.Idle,
            OverlayTopmostRequired = false
        };
        var transition = new UiSessionTransition(
            2,
            DateTime.UtcNow,
            new ExitPhotoFullscreenEvent(),
            previous,
            current);

        var decision = SessionTransitionPolicies.ResolveDecision(transition);

        decision.ZOrderDecision.ShouldTouchSurface.Should().BeFalse();
        decision.ZOrderDecision.RequestZOrderApply.Should().BeTrue();
        decision.ZOrderDecision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionWindowingReason.SceneChanged);
        decision.WidgetVisibilityReason.Should().Be(SessionFloatingWidgetVisibilityReason.None);
        decision.ApplyReason.Should().Be(SessionTransitionApplyReason.SceneChanged);
        decision.SurfaceReason.Should().Be(SessionTransitionSurfaceReason.NoSurfaceRetouchRequested);
    }

    [Fact]
    public void Resolve_ShouldDoNothing_WhenSceneAndTopmostRequirementStayUnchanged()
    {
        var state = UiSessionState.Default with
        {
            Scene = UiSceneKind.PhotoFullscreen,
            OverlayTopmostRequired = true,
            RollCallVisible = true,
            LauncherVisible = true,
            ToolbarVisible = true
        };
        var transition = new UiSessionTransition(
            3,
            DateTime.UtcNow,
            new MarkInkDirtyEvent(),
            state,
            state with { InkDirty = true });

        var decision = SessionTransitionPolicies.ResolveDecision(transition);

        decision.ZOrderDecision.ShouldTouchSurface.Should().BeFalse();
        decision.ZOrderDecision.RequestZOrderApply.Should().BeFalse();
        decision.ZOrderDecision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionWindowingReason.NoApplyRequested);
        decision.WidgetVisibilityReason.Should().Be(SessionFloatingWidgetVisibilityReason.None);
        decision.ApplyReason.Should().Be(SessionTransitionApplyReason.NoApplyRequested);
        decision.SurfaceReason.Should().Be(SessionTransitionSurfaceReason.NoSurfaceRetouchRequested);
    }

    [Fact]
    public void Resolve_ShouldRequestApplyWithForce_WhenWidgetVisibilityBecomesVisible()
    {
        var previous = UiSessionState.Default with
        {
            Scene = UiSceneKind.PhotoFullscreen,
            OverlayTopmostRequired = true,
            RollCallVisible = false,
            LauncherVisible = false,
            ToolbarVisible = false
        };
        var current = previous with
        {
            LauncherVisible = true
        };
        var transition = new UiSessionTransition(
            4,
            DateTime.UtcNow,
            new MarkInkSavedEvent(),
            previous,
            current);

        var decision = SessionTransitionPolicies.ResolveDecision(transition);

        decision.ZOrderDecision.ShouldTouchSurface.Should().BeFalse();
        decision.ZOrderDecision.RequestZOrderApply.Should().BeTrue();
        decision.ZOrderDecision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionWindowingReason.WidgetBecameVisible);
        decision.WidgetVisibilityReason.Should().Be(SessionFloatingWidgetVisibilityReason.LauncherBecameVisible);
        decision.ApplyReason.Should().Be(SessionTransitionApplyReason.WidgetBecameVisible);
        decision.SurfaceReason.Should().Be(SessionTransitionSurfaceReason.NoSurfaceRetouchRequested);
    }

    [Fact]
    public void Resolve_ShouldRequestApplyWithForce_WhenSceneChangesBetweenInteractiveScenes()
    {
        var previous = UiSessionState.Default with
        {
            Scene = UiSceneKind.PhotoFullscreen,
            OverlayTopmostRequired = true,
            RollCallVisible = true,
            LauncherVisible = true,
            ToolbarVisible = true
        };
        var current = previous with
        {
            Scene = UiSceneKind.Whiteboard
        };
        var transition = new UiSessionTransition(
            7,
            DateTime.UtcNow,
            new EnterWhiteboardEvent(),
            previous,
            current);

        var decision = SessionTransitionPolicies.ResolveDecision(transition);

        decision.ZOrderDecision.ShouldTouchSurface.Should().BeTrue();
        decision.ZOrderDecision.Surface.Should().Be(ZOrderSurface.Whiteboard);
        decision.ZOrderDecision.RequestZOrderApply.Should().BeTrue();
        decision.ZOrderDecision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionWindowingReason.SceneChanged);
        decision.ApplyReason.Should().Be(SessionTransitionApplyReason.SceneChanged);
        decision.SurfaceReason.Should().Be(SessionTransitionSurfaceReason.SurfaceRetouchRequested);
    }

    [Fact]
    public void Resolve_ShouldNotRequestApply_WhenWidgetVisibilityOnlyBecomesHidden()
    {
        var previous = UiSessionState.Default with
        {
            Scene = UiSceneKind.PhotoFullscreen,
            OverlayTopmostRequired = true,
            RollCallVisible = true,
            LauncherVisible = true,
            ToolbarVisible = true
        };
        var current = previous with
        {
            LauncherVisible = false
        };
        var transition = new UiSessionTransition(
            5,
            DateTime.UtcNow,
            new MarkInkSavedEvent(),
            previous,
            current);

        var decision = SessionTransitionPolicies.ResolveDecision(transition);

        decision.ZOrderDecision.ShouldTouchSurface.Should().BeFalse();
        decision.ZOrderDecision.RequestZOrderApply.Should().BeFalse();
        decision.ZOrderDecision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionWindowingReason.WidgetVisibilityChangedButNoWidgetBecameVisible);
        decision.WidgetVisibilityReason.Should().Be(SessionFloatingWidgetVisibilityReason.VisibilityChangedButNoWidgetBecameVisible);
        decision.ApplyReason.Should().Be(SessionTransitionApplyReason.WidgetVisibilityChangedButNoWidgetBecameVisible);
        decision.SurfaceReason.Should().Be(SessionTransitionSurfaceReason.NoSurfaceRetouchRequested);
    }

    [Fact]
    public void Resolve_ShouldMapResolveDecision()
    {
        var transition = new UiSessionTransition(
            8,
            DateTime.UtcNow,
            new EnterPhotoFullscreenEvent(PhotoSourceKind.Image),
            UiSessionState.Default,
            UiSessionState.Default with
            {
                Scene = UiSceneKind.PhotoFullscreen,
                OverlayTopmostRequired = true
            });

        var decision = SessionTransitionPolicies.ResolveWindowing(transition);
        decision.RequestZOrderApply.Should().BeTrue();
    }
}

public sealed class SessionTransitionZOrderPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_WhenSceneChangesToNonIdle()
    {
        var previous = UiSessionState.Default;
        var current = UiSessionReducer.Reduce(previous, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));

        var decision = SessionTransitionPolicies.ResolveZOrder(previous, current);
        decision.ShouldRetouchSurface.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionZOrderRetouchReason.SceneChangedToSurface);
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenSceneUnchanged()
    {
        var previous = UiSessionReducer.Reduce(UiSessionState.Default, new EnterWhiteboardEvent());
        var current = UiSessionReducer.Reduce(previous, new SwitchToolModeEvent(UiToolMode.Cursor));

        var decision = SessionTransitionPolicies.ResolveZOrder(previous, current);
        decision.ShouldRetouchSurface.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionZOrderRetouchReason.SceneUnchanged);
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenSceneChangesToIdle()
    {
        var previous = UiSessionReducer.Reduce(UiSessionState.Default, new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        var current = UiSessionReducer.Reduce(previous, new ExitPresentationFullscreenEvent());

        var decision = SessionTransitionPolicies.ResolveZOrder(previous, current);
        decision.ShouldRetouchSurface.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionZOrderRetouchReason.SceneChangedToNoneSurface);
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenSceneChangesBetweenNonIdleScenes()
    {
        var previous = UiSessionReducer.Reduce(UiSessionState.Default, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));
        var current = UiSessionReducer.Reduce(previous, new EnterWhiteboardEvent());

        var decision = SessionTransitionPolicies.ResolveZOrder(previous, current);
        decision.ShouldRetouchSurface.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionZOrderRetouchReason.SceneChangedToSurface);
    }

    [Fact]
    public void ShouldRetouchSurface_ShouldMapResolveDecision()
    {
        var previous = UiSessionState.Default;
        var current = UiSessionReducer.Reduce(previous, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));

        SessionTransitionPolicies.ShouldRetouchSurface(previous, current).Should().BeTrue();
    }
}
