namespace ClassroomToolkit.App.Windowing;

internal static class FloatingWindowRuntimeSnapshotPolicy
{
    public static FloatingWindowRuntimeSnapshot Resolve(
        bool overlayVisible,
        bool overlayActive,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible,
        bool imageManagerMinimized,
        bool launcherVisible)
    {
        return new FloatingWindowRuntimeSnapshot(
            OverlayVisible: overlayVisible,
            OverlayActive: overlayActive,
            PhotoActive: photoActive,
            PresentationFullscreen: presentationFullscreen,
            WhiteboardActive: whiteboardActive,
            ImageManagerVisible: imageManagerVisible && !imageManagerMinimized,
            LauncherVisible: launcherVisible);
    }
}
