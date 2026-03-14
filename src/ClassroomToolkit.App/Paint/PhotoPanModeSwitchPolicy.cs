namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanModeSwitchPolicy
{
    internal static bool ShouldEndPan(
        bool photoPanning,
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive)
    {
        if (!photoPanning)
        {
            return false;
        }

        return !StylusCursorPolicy.ShouldPanPhoto(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive);
    }
}
