namespace ClassroomToolkit.App.Windowing;

internal static class OverlayNavigationFocusSnapshotPolicy
{
    internal static OverlayNavigationFocusSnapshot Resolve(
        bool overlayVisible,
        bool overlayActive,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        return new OverlayNavigationFocusSnapshot(
            OverlayVisible: overlayVisible,
            OverlayActive: overlayActive,
            UtilityActivity: utilityActivity);
    }
}
