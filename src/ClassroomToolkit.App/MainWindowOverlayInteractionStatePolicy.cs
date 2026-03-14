namespace ClassroomToolkit.App;

internal static class MainWindowOverlayInteractionStatePolicy
{
    internal static MainWindowOverlayInteractionState Resolve(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive)
    {
        return new MainWindowOverlayInteractionState(
            OverlayVisible: overlayVisible,
            PhotoModeActive: photoModeActive,
            WhiteboardActive: whiteboardActive);
    }
}
