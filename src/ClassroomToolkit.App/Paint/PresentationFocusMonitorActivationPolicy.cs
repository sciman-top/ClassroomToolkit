namespace ClassroomToolkit.App.Paint;

internal static class PresentationFocusMonitorActivationPolicy
{
    internal static bool ShouldMonitor(
        bool overlayVisible,
        bool allowOffice,
        bool allowWps,
        bool photoFullscreenActive)
    {
        return overlayVisible && (allowOffice || allowWps || photoFullscreenActive);
    }
}
