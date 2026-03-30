namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PhotoOverlayReentryPlan(
    bool NormalizeWindowState,
    bool ActivateOverlay,
    bool ReturnEarly);

internal static class PhotoOverlayReentryPolicy
{
    public static PhotoOverlayReentryPlan Resolve(
        bool windowMinimized,
        bool photoModeActive,
        bool sameSourcePath)
    {
        var returnEarly = photoModeActive && sameSourcePath;
        return new PhotoOverlayReentryPlan(
            NormalizeWindowState: windowMinimized,
            ActivateOverlay: returnEarly,
            ReturnEarly: returnEarly);
    }
}
