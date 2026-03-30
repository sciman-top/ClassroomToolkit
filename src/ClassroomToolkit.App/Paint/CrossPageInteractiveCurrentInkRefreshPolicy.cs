namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveCurrentInkRefreshPolicy
{
    internal static bool ShouldRequest(
        bool interactiveSwitch,
        bool photoInkModeActive,
        PaintToolMode mode)
    {
        if (!interactiveSwitch || !photoInkModeActive)
        {
            return false;
        }

        return mode == PaintToolMode.Brush || mode == PaintToolMode.Eraser;
    }
}
