namespace ClassroomToolkit.App.Windowing;

internal enum ForegroundZOrderRetouchTrigger
{
    ToolbarInteraction = 0,
    ImageManagerActivated = 1,
    ImageManagerClosed = 2,
    ImageManagerStateChanged = 3,
    PhotoModeChanged = 4,
    PresentationFullscreenDetected = 5
}

internal enum ForegroundZOrderRetouchReason
{
    None = 0,
    ForceDisabledByDesign = 1,
    OverlayVisiblePresentation = 2,
    OverlayHiddenPresentation = 3
}

internal readonly record struct ForegroundZOrderRetouchDecision(
    bool ShouldForce,
    ForegroundZOrderRetouchReason Reason);

internal static class ForegroundZOrderRetouchPolicy
{
    internal static ForegroundZOrderRetouchDecision Resolve(
        ForegroundZOrderRetouchTrigger trigger,
        bool overlayVisible,
        bool photoModeActive = false,
        bool whiteboardActive = false)
    {
        _ = photoModeActive;
        _ = whiteboardActive;

        if (trigger == ForegroundZOrderRetouchTrigger.PresentationFullscreenDetected)
        {
            return overlayVisible
                ? new ForegroundZOrderRetouchDecision(
                    ShouldForce: true,
                    Reason: ForegroundZOrderRetouchReason.OverlayVisiblePresentation)
                : new ForegroundZOrderRetouchDecision(
                    ShouldForce: false,
                    Reason: ForegroundZOrderRetouchReason.OverlayHiddenPresentation);
        }

        return new ForegroundZOrderRetouchDecision(
            ShouldForce: false,
            Reason: ForegroundZOrderRetouchReason.ForceDisabledByDesign);
    }

    internal static bool ShouldForceOnToolbarInteraction(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.ToolbarInteraction,
            overlayVisible,
            photoModeActive,
            whiteboardActive).ShouldForce;
    }

    internal static bool ShouldForceOnImageManagerActivated(bool overlayVisible)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.ImageManagerActivated,
            overlayVisible).ShouldForce;
    }

    internal static bool ShouldForceOnImageManagerClosed(bool overlayVisible)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.ImageManagerClosed,
            overlayVisible).ShouldForce;
    }

    internal static bool ShouldForceOnImageManagerStateChanged(bool overlayVisible)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.ImageManagerStateChanged,
            overlayVisible).ShouldForce;
    }

    internal static bool ShouldForceOnPhotoModeChanged(bool photoModeActive)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.PhotoModeChanged,
            overlayVisible: false,
            photoModeActive: photoModeActive).ShouldForce;
    }

    internal static bool ShouldForceOnPresentationFullscreenDetected(bool overlayVisible)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.PresentationFullscreenDetected,
            overlayVisible).ShouldForce;
    }
}
