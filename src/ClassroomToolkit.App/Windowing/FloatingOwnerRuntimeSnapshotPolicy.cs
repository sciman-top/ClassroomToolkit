namespace ClassroomToolkit.App.Windowing;

internal static class FloatingOwnerRuntimeSnapshotPolicy
{
    public static FloatingOwnerRuntimeSnapshot Resolve(
        bool overlayVisible,
        bool toolbarOwnerAlreadyOverlay,
        bool rollCallOwnerAlreadyOverlay,
        bool imageManagerOwnerAlreadyOverlay)
    {
        return new FloatingOwnerRuntimeSnapshot(
            OverlayVisible: overlayVisible,
            ToolbarOwnerAlreadyOverlay: toolbarOwnerAlreadyOverlay,
            RollCallOwnerAlreadyOverlay: rollCallOwnerAlreadyOverlay,
            ImageManagerOwnerAlreadyOverlay: imageManagerOwnerAlreadyOverlay);
    }
}
