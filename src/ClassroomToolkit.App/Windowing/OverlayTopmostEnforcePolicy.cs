namespace ClassroomToolkit.App.Windowing;

internal static class OverlayTopmostEnforcePolicy
{
    internal static bool ResolveForPhotoFullscreen(bool overlayCurrentlyTopmost)
    {
        // Keep fullscreen transitions stable: only force native z-order replay
        // when overlay is not topmost yet.
        return !overlayCurrentlyTopmost;
    }
}
