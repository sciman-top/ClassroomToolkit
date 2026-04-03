namespace ClassroomToolkit.App.Paint;

internal static class PhotoWindowModeZOrderRetouchPolicy
{
    internal static bool ShouldRequest(bool photoModeActive, bool fullscreenChanged)
    {
        return photoModeActive && fullscreenChanged;
    }

    internal static bool ShouldForceEnforce(bool fullscreen)
    {
        return fullscreen;
    }
}
