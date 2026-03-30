namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostVisibilitySnapshotPolicy
{
    public static FloatingTopmostVisibilitySnapshot Resolve(
        bool toolbarVisible,
        bool rollCallVisible,
        bool launcherVisible,
        bool imageManagerVisible,
        bool overlayVisible)
    {
        return new FloatingTopmostVisibilitySnapshot(
            ToolbarVisible: toolbarVisible,
            RollCallVisible: rollCallVisible,
            LauncherVisible: launcherVisible,
            ImageManagerVisible: imageManagerVisible,
            OverlayVisible: overlayVisible);
    }
}
