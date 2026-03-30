namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveHoldDurationPolicy
{
    internal static int ResolveMs(
        int visibleNeighborPages,
        PaintToolMode mode = PaintToolMode.Cursor,
        int baseMs = CrossPageInteractiveHoldDurationDefaults.BaseMs,
        int extraPerNeighborMs = CrossPageInteractiveHoldDurationDefaults.ExtraPerNeighborMs,
        int maxMs = CrossPageInteractiveHoldDurationDefaults.MaxMs)
    {
        if (mode == PaintToolMode.Cursor)
        {
            return 0;
        }

        var normalizedVisible = Math.Max(0, visibleNeighborPages);
        var extraNeighbors = Math.Max(0, normalizedVisible - 1);
        var modeExtraMs = mode switch
        {
            PaintToolMode.Brush => CrossPageInteractiveHoldDurationDefaults.BrushModeExtraMs,
            PaintToolMode.Eraser => CrossPageInteractiveHoldDurationDefaults.EraserModeExtraMs,
            _ => 0
        };
        var value = baseMs + (extraNeighbors * Math.Max(0, extraPerNeighborMs)) + modeExtraMs;
        if (maxMs <= 0)
        {
            return Math.Max(1, value);
        }

        return Math.Clamp(value, 1, maxMs);
    }
}
