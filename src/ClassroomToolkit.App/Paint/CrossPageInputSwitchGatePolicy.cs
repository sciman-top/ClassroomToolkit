namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputSwitchGatePolicy
{
    internal static bool CanSwitchForInput(
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool boardActive,
        PaintToolMode mode,
        bool photoPanning,
        bool crossPageDragging)
    {
        if (!photoModeActive || !crossPageDisplayEnabled || boardActive)
        {
            return false;
        }
        if (mode != PaintToolMode.Brush && mode != PaintToolMode.Eraser)
        {
            return false;
        }

        // Avoid competing state updates between drag/pan and cross-page ink routing.
        return !photoPanning && !crossPageDragging;
    }
}
