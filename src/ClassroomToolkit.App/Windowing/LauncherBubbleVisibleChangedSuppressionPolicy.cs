namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleVisibleChangedSuppressionPolicy
{
    internal static int ResolveCooldownMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        int defaultMs = LauncherBubbleVisibleChangedSuppressionDefaults.TransitionCooldownMs,
        int interactiveMs = LauncherBubbleVisibleChangedSuppressionDefaults.InteractiveTransitionCooldownMs)
    {
        var interactiveScene = overlayVisible && (photoModeActive || whiteboardActive);
        return interactiveScene ? interactiveMs : defaultMs;
    }
}
