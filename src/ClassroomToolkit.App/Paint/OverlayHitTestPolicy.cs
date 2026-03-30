namespace ClassroomToolkit.App.Paint;

internal static class OverlayHitTestPolicy
{
    internal static bool ShouldEnableOverlayHitTest(
        PaintToolMode mode,
        bool photoModeActive,
        bool photoLoading)
    {
        if (photoLoading)
        {
            return false;
        }

        return mode != PaintToolMode.Cursor || photoModeActive;
    }
}
