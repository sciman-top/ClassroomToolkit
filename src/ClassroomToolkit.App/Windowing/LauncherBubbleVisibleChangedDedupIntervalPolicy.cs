namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleVisibleChangedDedupIntervalPolicy
{
    internal static int ResolveMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        int defaultMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs,
        int interactiveMs = FloatingInteractiveDedupIntervalDefaults.InteractiveMs)
    {
        return InteractiveSceneIntervalPolicy.ResolveMs(
            overlayVisible,
            photoModeActive,
            whiteboardActive,
            defaultMs,
            interactiveMs);
    }
}
