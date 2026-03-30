namespace ClassroomToolkit.App.Photos;

internal static class PhotoShowInkOverlayChangePolicy
{
    internal static bool ShouldApply(bool currentEnabled, bool nextEnabled)
    {
        return currentEnabled != nextEnabled;
    }
}
