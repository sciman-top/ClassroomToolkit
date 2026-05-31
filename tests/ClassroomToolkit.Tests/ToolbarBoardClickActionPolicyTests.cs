using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ToolbarBoardClickActionPolicyTests
{
    [Fact]
    public void Resolve_ShouldOpenActionsPopup_WhenPhotoModeIsActiveAndWhiteboardIsInactive()
    {
        var action = ToolbarBoardClickActionPolicy.Resolve(
            sessionCaptureWhiteboardActive: false,
            whiteboardActive: false,
            shouldEnterWhiteboardBySecondTap: false,
            directWhiteboardEntryArmed: false,
            resumeRegionCaptureArmed: false,
            regionCapturePending: false,
            photoModeActive: true);

        action.Should().Be(ToolbarBoardClickAction.OpenActionsPopup);
    }

    [Fact]
    public void Resolve_ShouldEnterWhiteboard_WhenPendingCaptureIsRetriedOutsidePhotoMode()
    {
        var action = ToolbarBoardClickActionPolicy.Resolve(
            sessionCaptureWhiteboardActive: false,
            whiteboardActive: false,
            shouldEnterWhiteboardBySecondTap: false,
            directWhiteboardEntryArmed: false,
            resumeRegionCaptureArmed: false,
            regionCapturePending: true,
            photoModeActive: false);

        action.Should().Be(ToolbarBoardClickAction.EnterWhiteboard);
    }

    [Fact]
    public void Resolve_ShouldOpenActionsPopup_WhenPendingCaptureIsInPhotoMode()
    {
        var action = ToolbarBoardClickActionPolicy.Resolve(
            sessionCaptureWhiteboardActive: false,
            whiteboardActive: false,
            shouldEnterWhiteboardBySecondTap: false,
            directWhiteboardEntryArmed: false,
            resumeRegionCaptureArmed: false,
            regionCapturePending: true,
            photoModeActive: true);

        action.Should().Be(ToolbarBoardClickAction.OpenActionsPopup);
    }

    [Fact]
    public void Resolve_ShouldExitExistingWhiteboardBeforeOpeningPopup()
    {
        var action = ToolbarBoardClickActionPolicy.Resolve(
            sessionCaptureWhiteboardActive: false,
            whiteboardActive: true,
            shouldEnterWhiteboardBySecondTap: false,
            directWhiteboardEntryArmed: false,
            resumeRegionCaptureArmed: false,
            regionCapturePending: false,
            photoModeActive: true);

        action.Should().Be(ToolbarBoardClickAction.ExitWhiteboard);
    }
}
