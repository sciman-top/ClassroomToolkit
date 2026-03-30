namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNavigationCurrentInkRefreshPolicy
{
    internal static bool ShouldRequest(
        bool pageChanged,
        bool interactiveSwitch,
        bool photoInkModeActive,
        PaintToolMode mode)
    {
        if (!pageChanged || !photoInkModeActive)
        {
            return false;
        }

        if (interactiveSwitch)
        {
            return mode == PaintToolMode.Brush || mode == PaintToolMode.Eraser;
        }

        return mode == PaintToolMode.Brush
            || mode == PaintToolMode.Eraser
            || mode == PaintToolMode.RegionErase;
    }
}
