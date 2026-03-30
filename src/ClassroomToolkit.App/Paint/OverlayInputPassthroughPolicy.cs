namespace ClassroomToolkit.App.Paint;

internal static class OverlayInputPassthroughPolicy
{
    private const double OpacityEpsilon = OverlayInputPassthroughDefaults.OpacityEpsilon;

    internal static bool ShouldEnable(
        PaintToolMode mode,
        double boardOpacity,
        bool photoModeActive)
    {
        if (photoModeActive)
        {
            return false;
        }

        if (mode != PaintToolMode.Cursor)
        {
            return false;
        }

        return boardOpacity <= OpacityEpsilon;
    }
}
