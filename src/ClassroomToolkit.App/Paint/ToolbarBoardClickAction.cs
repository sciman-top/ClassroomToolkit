namespace ClassroomToolkit.App.Paint;

internal enum ToolbarBoardClickAction
{
    OpenActionsPopup = 0,
    ExitSessionCaptureWhiteboard = 1,
    ExitWhiteboard = 2,
    EnterWhiteboard = 3
}

internal static class ToolbarBoardClickActionPolicy
{
    internal static ToolbarBoardClickAction Resolve(
        bool sessionCaptureWhiteboardActive,
        bool whiteboardActive,
        bool shouldEnterWhiteboardBySecondTap,
        bool directWhiteboardEntryArmed,
        bool resumeRegionCaptureArmed,
        bool regionCapturePending,
        bool photoModeActive)
    {
        if (sessionCaptureWhiteboardActive)
        {
            return ToolbarBoardClickAction.ExitSessionCaptureWhiteboard;
        }

        if (whiteboardActive)
        {
            return ToolbarBoardClickAction.ExitWhiteboard;
        }

        if (shouldEnterWhiteboardBySecondTap)
        {
            return ToolbarBoardClickAction.EnterWhiteboard;
        }

        if ((directWhiteboardEntryArmed || resumeRegionCaptureArmed || regionCapturePending)
            && !photoModeActive)
        {
            return ToolbarBoardClickAction.EnterWhiteboard;
        }

        return ToolbarBoardClickAction.OpenActionsPopup;
    }
}
