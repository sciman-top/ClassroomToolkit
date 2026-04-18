using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresentationFocusRestorePolicyTests
{
    [Fact]
    public void CanRestore_ShouldReturnTrue_WhenCursorHybridAndForegroundAllowed()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        var result = PresentationFocusRestorePolicy.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRestore_ShouldReturnFalse_WhenToolModeIsDraw()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionState.Default,
            new EnterPresentationFullscreenEvent(PresentationSourceKind.Wps));

        var result = PresentationFocusRestorePolicy.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRestore_ShouldReturnFalse_WhenPhotoOrBoardActive()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        var photo = PresentationFocusRestorePolicy.CanRestore(
            state,
            photoModeActive: true,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: false);
        var board = PresentationFocusRestorePolicy.CanRestore(
            state,
            photoModeActive: false,
            boardActive: true,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: false);

        photo.Should().BeFalse();
        board.Should().BeFalse();
    }

    [Fact]
    public void CanRestore_ShouldRespectFullscreenRequirement()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.Wps));

        var result = PresentationFocusRestorePolicy.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: false,
            requireFullscreen: true,
            forceForeground: true,
            foregroundOwnedByCurrentProcess: false,
            dragOperationActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRestore_ShouldAllow_WhenHookOnlyAndForcedForeground()
    {
        var state = UiSessionState.Default with
        {
            ToolMode = UiToolMode.Cursor,
            NavigationMode = UiNavigationMode.HookOnly,
            Scene = UiSceneKind.PresentationFullscreen,
            FocusOwner = UiFocusOwner.Presentation
        };

        var result = PresentationFocusRestorePolicy.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: true,
            foregroundOwnedByCurrentProcess: false,
            dragOperationActive: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRestore_ShouldReturnFalse_WhenWindowDragOperationIsActive()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        var result = PresentationFocusRestorePolicy.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRestore_ShouldFollowWindowDragOperationState()
    {
        var state = UiSessionReducer.Reduce(
            UiSessionReducer.Reduce(UiSessionState.Default, new SwitchToolModeEvent(UiToolMode.Cursor)),
            new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));

        using var dragScope = WindowDragOperationState.Begin();
        var result = PresentationFocusRestorePolicy.CanRestore(
            state,
            photoModeActive: false,
            boardActive: false,
            isVisible: true,
            presentationAllowed: true,
            targetIsValid: true,
            targetIsSlideshow: true,
            targetIsFullscreen: true,
            requireFullscreen: true,
            forceForeground: false,
            foregroundOwnedByCurrentProcess: true,
            dragOperationActive: WindowDragOperationState.IsActive);

        result.Should().BeFalse();
    }
}
