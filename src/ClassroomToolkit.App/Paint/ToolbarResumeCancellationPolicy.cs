namespace ClassroomToolkit.App.Paint;

internal static class ToolbarResumeCancellationPolicy
{
    internal static bool ShouldCancelPendingResumeOnToolbarPress(
        bool resumeArmed,
        bool pressedToolbarButton,
        bool pressedBoardButton)
    {
        return resumeArmed && pressedToolbarButton && !pressedBoardButton;
    }
}
