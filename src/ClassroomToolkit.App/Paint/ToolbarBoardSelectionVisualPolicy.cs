namespace ClassroomToolkit.App.Paint;

internal static class ToolbarBoardSelectionVisualPolicy
{
    internal static bool Resolve(
        bool boardActive,
        bool overlayWhiteboardActive,
        bool sessionCaptureWhiteboardActive,
        bool directWhiteboardEntryArmed,
        bool regionCapturePending)
    {
        if (boardActive
            || overlayWhiteboardActive
            || sessionCaptureWhiteboardActive
            || directWhiteboardEntryArmed
            || regionCapturePending)
        {
            return true;
        }

        return false;
    }
}
