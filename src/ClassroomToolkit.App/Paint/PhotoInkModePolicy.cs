namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkModePolicy
{
    internal static bool IsActive(bool photoModeActive, bool boardActive)
    {
        return photoModeActive && !boardActive;
    }
}
