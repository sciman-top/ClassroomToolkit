namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanMouseRoutingPolicy
{
    internal static bool ShouldHandlePhotoPan(
        bool photoPanning,
        bool photoModeActive,
        PaintToolMode mode,
        bool inkOperationActive)
    {
        return photoPanning
               && photoModeActive
               && mode == PaintToolMode.Cursor
               && !inkOperationActive;
    }
}
