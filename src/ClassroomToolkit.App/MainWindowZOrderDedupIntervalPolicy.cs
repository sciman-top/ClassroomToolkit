using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

internal static class MainWindowZOrderDedupIntervalPolicy
{
    internal static int ResolveSurfaceDecisionIntervalMs(MainWindowOverlayInteractionState interactionState)
    {
        return SurfaceZOrderDecisionDedupIntervalPolicy.ResolveMs(
            overlayVisible: interactionState.OverlayVisible,
            photoModeActive: interactionState.PhotoModeActive,
            whiteboardActive: interactionState.WhiteboardActive);
    }

    internal static int ResolveRequestIntervalMs(MainWindowOverlayInteractionState interactionState)
    {
        return ZOrderRequestDedupIntervalPolicy.ResolveMs(
            overlayVisible: interactionState.OverlayVisible,
            photoModeActive: interactionState.PhotoModeActive,
            whiteboardActive: interactionState.WhiteboardActive);
    }
}
