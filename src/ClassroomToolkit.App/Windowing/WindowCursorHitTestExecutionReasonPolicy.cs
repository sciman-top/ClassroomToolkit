namespace ClassroomToolkit.App.Windowing;

internal static class WindowCursorHitTestExecutionReasonPolicy
{
    internal static string ResolveTag(WindowCursorHitTestExecutionReason reason)
    {
        return reason switch
        {
            WindowCursorHitTestExecutionReason.InvalidWindowHandle => "invalid-window-handle",
            WindowCursorHitTestExecutionReason.CursorUnavailable => "cursor-unavailable",
            WindowCursorHitTestExecutionReason.WindowRectUnavailable => "window-rect-unavailable",
            WindowCursorHitTestExecutionReason.HitTestCompleted => "hit-test-completed",
            _ => "none"
        };
    }
}
