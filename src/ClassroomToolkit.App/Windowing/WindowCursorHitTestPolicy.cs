namespace ClassroomToolkit.App.Windowing;

internal enum WindowCursorHitTestReason
{
    None = 0,
    InsideBounds = 1,
    OutsideBounds = 2
}

internal readonly record struct WindowCursorHitTestDecision(
    bool IsInside,
    WindowCursorHitTestReason Reason);

internal static class WindowCursorHitTestPolicy
{
    internal static WindowCursorHitTestDecision Resolve(
        int cursorX,
        int cursorY,
        int left,
        int top,
        int right,
        int bottom)
    {
        var inside = cursorX >= left
            && cursorX <= right
            && cursorY >= top
            && cursorY <= bottom;
        return inside
            ? new WindowCursorHitTestDecision(
                IsInside: true,
                Reason: WindowCursorHitTestReason.InsideBounds)
            : new WindowCursorHitTestDecision(
                IsInside: false,
                Reason: WindowCursorHitTestReason.OutsideBounds);
    }

    internal static bool IsInside(
        int cursorX,
        int cursorY,
        int left,
        int top,
        int right,
        int bottom)
    {
        return Resolve(cursorX, cursorY, left, top, right, bottom).IsInside;
    }
}
