namespace ClassroomToolkit.App.Windowing;

internal static class PhotoModeSurfaceTransitionPolicy
{
    internal static SurfaceZOrderDecision Resolve(
        PhotoModeSurfaceTransitionKind kind,
        PhotoModeSurfaceTransitionContext context)
    {
        return Resolve(
            kind,
            context.PhotoModeActive,
            context.RequestZOrderApply,
            context.ForceEnforceZOrder,
            context.OverlayVisible);
    }

    internal static SurfaceZOrderDecision Resolve(
        PhotoModeSurfaceTransitionKind kind,
        bool photoModeActive,
        bool requestZOrderApply,
        bool forceEnforceZOrder,
        bool overlayVisible)
    {
        return kind switch
        {
            PhotoModeSurfaceTransitionKind.PhotoModeChanged => ResolvePhotoModeChanged(
                photoModeActive,
                requestZOrderApply,
                forceEnforceZOrder),
            PhotoModeSurfaceTransitionKind.PresentationFullscreenDetected => ResolvePresentationFullscreenDetected(
                overlayVisible),
            _ => ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: false)
        };
    }

    private static SurfaceZOrderDecision ResolvePhotoModeChanged(
        bool photoModeActive,
        bool requestZOrderApply,
        bool forceEnforceZOrder)
    {
        var baseForce = ForegroundZOrderRetouchPolicy.ShouldForceOnPhotoModeChanged(photoModeActive);

        return new SurfaceZOrderDecision(
            ShouldTouchSurface: requestZOrderApply && photoModeActive,
            Surface: photoModeActive ? ZOrderSurface.PhotoFullscreen : ZOrderSurface.None,
            RequestZOrderApply: requestZOrderApply,
            ForceEnforceZOrder: forceEnforceZOrder || baseForce);
    }

    private static SurfaceZOrderDecision ResolvePresentationFullscreenDetected(bool overlayVisible)
    {
        var noTouch = ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: overlayVisible);
        return noTouch with
        {
            ForceEnforceZOrder = ForegroundZOrderRetouchPolicy.ShouldForceOnPresentationFullscreenDetected(
                overlayVisible)
        };
    }
}
