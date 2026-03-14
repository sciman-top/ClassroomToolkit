using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

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

        var decision = SessionTransitionWindowingPolicy.ResolveDecision(transition);

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

        var decision = SessionTransitionWindowingPolicy.ResolveDecision(transition);

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

        var decision = SessionTransitionWindowingPolicy.ResolveDecision(transition);

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

        var decision = SessionTransitionWindowingPolicy.ResolveDecision(transition);

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

        var decision = SessionTransitionWindowingPolicy.ResolveDecision(transition);

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

        var decision = SessionTransitionWindowingPolicy.ResolveDecision(transition);

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

        var decision = SessionTransitionWindowingPolicy.Resolve(transition);
        decision.RequestZOrderApply.Should().BeTrue();
    }
}
