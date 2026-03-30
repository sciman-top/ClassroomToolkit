using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionReducerTests
{
    [Fact]
    public void Reduce_ShouldThrow_WhenCurrentStateIsNull()
    {
        Action act = () => UiSessionReducer.Reduce(null!, new MarkInkDirtyEvent());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Reduce_ShouldThrow_WhenSessionEventIsNull()
    {
        Action act = () => UiSessionReducer.Reduce(UiSessionState.Default, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Reduce_ShouldEnableOverlayAndHybridNavigation_WhenEnterPresentationInCursorMode()
    {
        var cursorState = UiSessionReducer.Reduce(
            UiSessionState.Default,
            new SwitchToolModeEvent(UiToolMode.Cursor));

        var next = UiSessionReducer.Reduce(
            cursorState,
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        next.Scene.Should().Be(UiSceneKind.PresentationFullscreen);
        next.NavigationMode.Should().Be(UiNavigationMode.Hybrid);
        next.OverlayTopmostRequired.Should().BeTrue();
        next.InkVisibility.Should().Be(UiInkVisibility.VisibleReadOnly);
    }

    [Fact]
    public void Reduce_ShouldReturnToIdle_WhenExitPhotoFullscreen()
    {
        var entered = UiSessionReducer.Reduce(
            UiSessionState.Default,
            new EnterPhotoFullscreenEvent(PhotoSourceKind.Pdf));

        var exited = UiSessionReducer.Reduce(
            entered,
            new ExitPhotoFullscreenEvent());

        exited.Scene.Should().Be(UiSceneKind.Idle);
        exited.OverlayTopmostRequired.Should().BeFalse();
        exited.RollCallVisible.Should().BeFalse();
        exited.LauncherVisible.Should().BeFalse();
        exited.ToolbarVisible.Should().BeFalse();
    }

    [Fact]
    public void Reduce_ShouldKeepNavigationDisabled_WhenWhiteboardInCursorMode()
    {
        var cursorState = UiSessionReducer.Reduce(
            UiSessionState.Default,
            new SwitchToolModeEvent(UiToolMode.Cursor));

        var next = UiSessionReducer.Reduce(
            cursorState,
            new EnterWhiteboardEvent());

        next.Scene.Should().Be(UiSceneKind.Whiteboard);
        next.NavigationMode.Should().Be(UiNavigationMode.Disabled);
        next.InkVisibility.Should().Be(UiInkVisibility.VisibleReadOnly);
    }

    [Fact]
    public void Reduce_ShouldRestorePhotoScene_WhenExitWhiteboardWithPhotoResume()
    {
        var entered = UiSessionReducer.Reduce(
            UiSessionState.Default,
            new EnterWhiteboardEvent());

        var exited = UiSessionReducer.Reduce(
            entered,
            new ExitWhiteboardEvent(UiSceneKind.PhotoFullscreen, PhotoSourceKind.Pdf));

        exited.Scene.Should().Be(UiSceneKind.PhotoFullscreen);
        exited.FocusOwner.Should().Be(UiFocusOwner.Photo);
    }

    [Fact]
    public void Reduce_ShouldRestorePresentationScene_WhenExitWhiteboardWithPresentationResume()
    {
        var entered = UiSessionReducer.Reduce(
            UiSessionState.Default,
            new EnterWhiteboardEvent());

        var exited = UiSessionReducer.Reduce(
            entered,
            new ExitWhiteboardEvent(
                ResumeScene: UiSceneKind.PresentationFullscreen,
                PresentationSource: PresentationSourceKind.Wps));

        exited.Scene.Should().Be(UiSceneKind.PresentationFullscreen);
        exited.FocusOwner.Should().Be(UiFocusOwner.Presentation);
    }

    [Fact]
    public void Reduce_ShouldIgnoreExitWhiteboard_WhenCurrentSceneIsNotWhiteboard()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionState.Default,
            new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));

        var next = UiSessionReducer.Reduce(
            state,
            new ExitWhiteboardEvent(
                ResumeScene: UiSceneKind.PresentationFullscreen,
                PresentationSource: PresentationSourceKind.Wps));

        next.Scene.Should().Be(UiSceneKind.PhotoFullscreen);
        next.FocusOwner.Should().Be(UiFocusOwner.Photo);
    }

    [Fact]
    public void Reduce_ShouldToggleInkDirtyFlag()
    {
        var dirty = UiSessionReducer.Reduce(UiSessionState.Default, new MarkInkDirtyEvent());
        var saved = UiSessionReducer.Reduce(dirty, new MarkInkSavedEvent());

        dirty.InkDirty.Should().BeTrue();
        saved.InkDirty.Should().BeFalse();
    }
}
