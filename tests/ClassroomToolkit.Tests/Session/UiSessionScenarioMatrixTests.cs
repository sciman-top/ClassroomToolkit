using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionScenarioMatrixTests
{
    [Theory]
    [InlineData(UiSceneKind.Idle, UiSceneKind.PresentationFullscreen, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.Idle, UiSceneKind.PhotoFullscreen, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.Idle, UiSceneKind.Whiteboard, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiSceneKind.PhotoFullscreen, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiSceneKind.Whiteboard, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.Whiteboard, UiSceneKind.PresentationFullscreen, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiSceneKind.Idle, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiSceneKind.Idle, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.Whiteboard, UiSceneKind.Idle, UiToolMode.Cursor)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiSceneKind.PhotoFullscreen, UiToolMode.Draw)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiSceneKind.Whiteboard, UiToolMode.Draw)]
    [InlineData(UiSceneKind.Whiteboard, UiSceneKind.PresentationFullscreen, UiToolMode.Draw)]
    [InlineData(UiSceneKind.PresentationFullscreen, UiSceneKind.Idle, UiToolMode.Draw)]
    [InlineData(UiSceneKind.PhotoFullscreen, UiSceneKind.Idle, UiToolMode.Draw)]
    [InlineData(UiSceneKind.Whiteboard, UiSceneKind.Idle, UiToolMode.Draw)]
    public void TransitionMatrix_ShouldKeepSessionContract(
        UiSceneKind from,
        UiSceneKind to,
        UiToolMode toolMode)
    {
        var state = UiSessionState.Default;
        state = UiSessionReducer.Reduce(state, new SwitchToolModeEvent(toolMode));
        state = EnterScene(state, from);
        state = TransitionTo(state, from, to);

        state.Scene.Should().Be(to);
        state.FocusOwner.Should().Be(ExpectedFocusOwner(to));
        state.NavigationMode.Should().Be(UiSessionNavigationPolicy.Resolve(to, toolMode));
        UiSessionInvariants.Validate(state).Should().BeEmpty();
    }

    private static UiSessionState EnterScene(UiSessionState state, UiSceneKind scene)
    {
        return scene switch
        {
            UiSceneKind.PresentationFullscreen => UiSessionReducer.Reduce(state, new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint)),
            UiSceneKind.PhotoFullscreen => UiSessionReducer.Reduce(state, new EnterPhotoFullscreenEvent(PhotoSourceKind.Pdf)),
            UiSceneKind.Whiteboard => UiSessionReducer.Reduce(state, new EnterWhiteboardEvent()),
            _ => state
        };
    }

    private static UiSessionState TransitionTo(UiSessionState state, UiSceneKind from, UiSceneKind to)
    {
        if (to == UiSceneKind.Idle)
        {
            return from switch
            {
                UiSceneKind.PresentationFullscreen => UiSessionReducer.Reduce(state, new ExitPresentationFullscreenEvent()),
                UiSceneKind.PhotoFullscreen => UiSessionReducer.Reduce(state, new ExitPhotoFullscreenEvent()),
                UiSceneKind.Whiteboard => UiSessionReducer.Reduce(state, new ExitWhiteboardEvent(UiSceneKind.Idle)),
                _ => state
            };
        }

        return to switch
        {
            UiSceneKind.PresentationFullscreen => UiSessionReducer.Reduce(state, new EnterPresentationFullscreenEvent(PresentationSourceKind.Wps)),
            UiSceneKind.PhotoFullscreen => UiSessionReducer.Reduce(state, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image)),
            UiSceneKind.Whiteboard => UiSessionReducer.Reduce(state, new EnterWhiteboardEvent()),
            _ => state
        };
    }

    private static UiFocusOwner ExpectedFocusOwner(UiSceneKind scene)
    {
        return scene switch
        {
            UiSceneKind.PresentationFullscreen => UiFocusOwner.Presentation,
            UiSceneKind.PhotoFullscreen => UiFocusOwner.Photo,
            UiSceneKind.Whiteboard => UiFocusOwner.Whiteboard,
            _ => UiFocusOwner.None
        };
    }
}
