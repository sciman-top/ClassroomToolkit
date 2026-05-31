namespace ClassroomToolkit.App.Paint;

internal static class PresentationOverlayRetouchPolicy
{
    internal static bool ShouldRequest(
        bool presentationActionApplied,
        bool overlayVisible,
        bool presentationFullscreenActive)
    {
        return presentationActionApplied
            && overlayVisible
            && presentationFullscreenActive;
    }
}
