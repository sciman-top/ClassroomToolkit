using ClassroomToolkit.App.Session;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Session;

public sealed class UiSessionWhiteboardResumeMatrixTests
{
    [Theory]
    [InlineData(UiToolMode.Cursor, UiSceneKind.PresentationFullscreen, UiFocusOwner.Presentation, UiNavigationMode.Hybrid, true)]
    [InlineData(UiToolMode.Cursor, UiSceneKind.PhotoFullscreen, UiFocusOwner.Photo, UiNavigationMode.MessageOnly, true)]
    [InlineData(UiToolMode.Draw, UiSceneKind.PresentationFullscreen, UiFocusOwner.Presentation, UiNavigationMode.HookOnly, true)]
    [InlineData(UiToolMode.Draw, UiSceneKind.PhotoFullscreen, UiFocusOwner.Photo, UiNavigationMode.Disabled, true)]
    [InlineData(UiToolMode.Cursor, UiSceneKind.Idle, UiFocusOwner.None, UiNavigationMode.Disabled, false)]
    [InlineData(UiToolMode.Draw, UiSceneKind.Idle, UiFocusOwner.None, UiNavigationMode.Disabled, false)]
    public void ExitWhiteboard_ShouldRestoreExpectedSessionContract(
        UiToolMode toolMode,
        UiSceneKind resumeScene,
        UiFocusOwner expectedFocusOwner,
        UiNavigationMode expectedNavigationMode,
        bool expectedOverlayVisible)
    {
        var state = UiSessionState.Default;
        state = UiSessionReducer.Reduce(state, new SwitchToolModeEvent(toolMode));
        state = UiSessionReducer.Reduce(state, new EnterWhiteboardEvent());

        var resumed = UiSessionReducer.Reduce(
            state,
            new ExitWhiteboardEvent(resumeScene, PhotoSourceKind.Pdf, PresentationSourceKind.Wps));

        resumed.Scene.Should().Be(resumeScene);
        resumed.FocusOwner.Should().Be(expectedFocusOwner);
        resumed.NavigationMode.Should().Be(expectedNavigationMode);
        resumed.RollCallVisible.Should().Be(expectedOverlayVisible);
        resumed.LauncherVisible.Should().Be(expectedOverlayVisible);
        resumed.ToolbarVisible.Should().Be(expectedOverlayVisible);
        UiSessionInvariants.Validate(resumed).Should().BeEmpty();
    }
}
