namespace ClassroomToolkit.App.Windowing;

internal static class SurfaceZOrderDecisionDedupIntervalPolicy
{
    internal static int ResolveMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        int defaultMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs,
        int interactiveMs = FloatingInteractiveDedupIntervalDefaults.InteractiveMs)
    {
        var interactiveScene = overlayVisible && (photoModeActive || whiteboardActive);
        return interactiveScene ? interactiveMs : defaultMs;
    }
}
