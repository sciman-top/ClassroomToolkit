using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionTransitionMatrixTests
{
    [Fact]
    public void TransitionChain_ShouldReturnToIdle_AndKeepInvariants()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new SwitchToolModeEvent(UiToolMode.Cursor));
        coordinator.Dispatch(new EnterPresentationFullscreenEvent(PresentationSourceKind.Wps));
        coordinator.Dispatch(new EnterPhotoFullscreenEvent(PhotoSourceKind.Pdf));
        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.Dispatch(new ExitWhiteboardEvent());
        coordinator.Dispatch(new ExitPhotoFullscreenEvent());
        coordinator.Dispatch(new ExitPresentationFullscreenEvent());

        coordinator.CurrentState.Scene.Should().Be(UiSceneKind.Idle);
        coordinator.CurrentState.FocusOwner.Should().Be(UiFocusOwner.None);
        coordinator.LastViolations.Should().BeEmpty();
    }

    [Fact]
    public void CursorMode_ShouldProduceExpectedNavigationMode_PerScene()
    {
        var state = UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor));

        var presentation = UiSessionReducer.Reduce(state, new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        presentation.NavigationMode.Should().Be(UiNavigationMode.Hybrid);

        var photo = UiSessionReducer.Reduce(state, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));
        photo.NavigationMode.Should().Be(UiNavigationMode.MessageOnly);

        var whiteboard = UiSessionReducer.Reduce(state, new EnterWhiteboardEvent());
        whiteboard.NavigationMode.Should().Be(UiNavigationMode.Disabled);
    }

    [Theory]
    [InlineData(PresentationSourceKind.PowerPoint)]
    [InlineData(PresentationSourceKind.Wps)]
    public void CursorMode_ShouldUseHybridNavigation_InPresentation_ForAllSources(PresentationSourceKind source)
    {
        var state = UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor));
        var presentation = UiSessionReducer.Reduce(state, new EnterPresentationFullscreenEvent(source));

        presentation.NavigationMode.Should().Be(UiNavigationMode.Hybrid);
        presentation.FocusOwner.Should().Be(UiFocusOwner.Presentation);
        presentation.Scene.Should().Be(UiSceneKind.PresentationFullscreen);
    }

    [Fact]
    public void DrawMode_ShouldKeepNavigationDisabled_InAllScenes()
    {
        var state = UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Draw));

        var presentation = UiSessionReducer.Reduce(state, new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        var photo = UiSessionReducer.Reduce(state, new EnterPhotoFullscreenEvent(PhotoSourceKind.Pdf));
        var whiteboard = UiSessionReducer.Reduce(state, new EnterWhiteboardEvent());

        presentation.NavigationMode.Should().Be(UiNavigationMode.HookOnly);
        photo.NavigationMode.Should().Be(UiNavigationMode.Disabled);
        whiteboard.NavigationMode.Should().Be(UiNavigationMode.Disabled);

        presentation.InkVisibility.Should().Be(UiInkVisibility.VisibleEditable);
        photo.InkVisibility.Should().Be(UiInkVisibility.VisibleEditable);
        whiteboard.InkVisibility.Should().Be(UiInkVisibility.VisibleEditable);
    }

    [Fact]
    public void InkDirtyFlag_ShouldNotBeReset_BySceneOrToolTransitions()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new MarkInkDirtyEvent());
        coordinator.Dispatch(new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        coordinator.Dispatch(new SwitchToolModeEvent(UiToolMode.Cursor));
        coordinator.Dispatch(new EnterPhotoFullscreenEvent(PhotoSourceKind.Pdf));
        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.Dispatch(new ExitWhiteboardEvent());
        coordinator.Dispatch(new ExitPhotoFullscreenEvent());
        coordinator.Dispatch(new ExitPresentationFullscreenEvent());

        coordinator.CurrentState.InkDirty.Should().BeTrue();
        coordinator.LastViolations.Should().BeEmpty();
    }

    [Fact]
    public void InkDirtyFlag_ShouldClearOnlyAfterSavedEvent()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new MarkInkDirtyEvent());
        coordinator.Dispatch(new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));
        coordinator.CurrentState.InkDirty.Should().BeTrue();

        coordinator.Dispatch(new MarkInkSavedEvent());
        coordinator.CurrentState.InkDirty.Should().BeFalse();
        coordinator.LastViolations.Should().BeEmpty();
    }

    [Fact]
    public void WhiteboardExitInPhotoContext_ShouldReturnToPhotoScene()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new EnterPhotoFullscreenEvent(PhotoSourceKind.Pdf));
        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.Dispatch(new ExitWhiteboardEvent(UiSceneKind.PhotoFullscreen, PhotoSourceKind.Pdf));

        coordinator.CurrentState.Scene.Should().Be(UiSceneKind.PhotoFullscreen);
        coordinator.CurrentState.FocusOwner.Should().Be(UiFocusOwner.Photo);
        coordinator.CurrentState.NavigationMode.Should().Be(UiNavigationMode.Disabled);
        coordinator.LastViolations.Should().BeEmpty();
    }

    [Fact]
    public void WhiteboardExitWithoutResumeScene_ShouldReturnToIdle()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.Dispatch(new ExitWhiteboardEvent());

        coordinator.CurrentState.Scene.Should().Be(UiSceneKind.Idle);
        coordinator.CurrentState.FocusOwner.Should().Be(UiFocusOwner.None);
        coordinator.LastViolations.Should().BeEmpty();
    }

    [Fact]
    public void WhiteboardExitWithPresentationResume_ShouldReturnToPresentationScene()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.Dispatch(new ExitWhiteboardEvent(
            ResumeScene: UiSceneKind.PresentationFullscreen,
            PresentationSource: PresentationSourceKind.PowerPoint));

        coordinator.CurrentState.Scene.Should().Be(UiSceneKind.PresentationFullscreen);
        coordinator.CurrentState.FocusOwner.Should().Be(UiFocusOwner.Presentation);
        coordinator.CurrentState.NavigationMode.Should().Be(UiNavigationMode.HookOnly);
        coordinator.LastViolations.Should().BeEmpty();
    }

    [Fact]
    public void WhiteboardExitWithIdleResume_ShouldHideOverlayWidgets()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.Dispatch(new ExitWhiteboardEvent(UiSceneKind.Idle));

        coordinator.CurrentState.Scene.Should().Be(UiSceneKind.Idle);
        coordinator.CurrentState.OverlayTopmostRequired.Should().BeFalse();
        coordinator.CurrentState.RollCallVisible.Should().BeFalse();
        coordinator.CurrentState.LauncherVisible.Should().BeFalse();
        coordinator.CurrentState.ToolbarVisible.Should().BeFalse();
        coordinator.LastViolations.Should().BeEmpty();
    }

    [Fact]
    public void MultiSceneRoundTrip_WithSourceSwitches_ShouldKeepInvariantSet()
    {
        var coordinator = new SessionCoordinator(new NoopEffectRunner());

        coordinator.Dispatch(new SwitchToolModeEvent(UiToolMode.Cursor));
        coordinator.Dispatch(new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.Dispatch(new ExitWhiteboardEvent(
            ResumeScene: UiSceneKind.PresentationFullscreen,
            PresentationSource: PresentationSourceKind.PowerPoint));
        coordinator.Dispatch(new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));
        coordinator.Dispatch(new EnterWhiteboardEvent());
        coordinator.Dispatch(new ExitWhiteboardEvent(
            ResumeScene: UiSceneKind.PhotoFullscreen,
            PhotoSource: PhotoSourceKind.Image));
        coordinator.Dispatch(new ExitPhotoFullscreenEvent());
        coordinator.Dispatch(new ExitPresentationFullscreenEvent());

        coordinator.CurrentState.Scene.Should().Be(UiSceneKind.Idle);
        coordinator.CurrentState.FocusOwner.Should().Be(UiFocusOwner.None);
        coordinator.CurrentState.NavigationMode.Should().Be(UiNavigationMode.Disabled);
        coordinator.LastViolations.Should().BeEmpty();
    }

    private sealed class NoopEffectRunner : IUiSessionEffectRunner
    {
        public void Run(UiSessionTransition transition)
        {
        }
    }
}
