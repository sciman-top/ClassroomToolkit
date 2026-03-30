namespace ClassroomToolkit.App.Windowing;

internal static class WindowCursorHitTestReasonPolicy
{
    internal static string ResolveTag(WindowCursorHitTestReason reason)
    {
        return reason switch
        {
            WindowCursorHitTestReason.InsideBounds => "inside-bounds",
            WindowCursorHitTestReason.OutsideBounds => "outside-bounds",
            _ => "none"
        };
    }
}
