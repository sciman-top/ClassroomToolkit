using System;

namespace ClassroomToolkit.App.Paint;

internal static class PresentationWheelInkConflictPolicy
{
    internal static bool ShouldSuppress(
        PaintToolMode mode,
        DateTime lastInkInputUtc,
        DateTime nowUtc,
        int suppressWindowMs)
    {
        if (mode == PaintToolMode.Cursor)
        {
            return false;
        }

        if (lastInkInputUtc == InkRuntimeTimingDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        var windowMs = Math.Max(0, suppressWindowMs);
        return (nowUtc - lastInkInputUtc).TotalMilliseconds < windowMs;
    }
}

