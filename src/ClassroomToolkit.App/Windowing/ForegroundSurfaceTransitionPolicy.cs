namespace ClassroomToolkit.App.Windowing;

internal static class ForegroundSurfaceTransitionPolicy
{
    internal static SurfaceZOrderDecision Resolve(
        ForegroundSurfaceTransitionKind kind,
        bool suppressNextApply,
        ForegroundSurfaceActivityState activityState,
        ZOrderSurface surface)
    {
        return Resolve(
            kind,
            suppressNextApply,
            activityState.OverlayExists,
            activityState.PhotoModeActive,
            activityState.WhiteboardActive,
            surface);
    }

    internal static SurfaceZOrderDecision Resolve(
        ForegroundSurfaceTransitionKind kind,
        bool suppressNextApply,
        bool overlayExists,
        bool photoModeActive,
        bool whiteboardActive,
        ZOrderSurface surface)
    {
        return kind switch
        {
            ForegroundSurfaceTransitionKind.OverlayActivated => ResolveOverlayActivated(
                suppressNextApply,
                overlayExists,
                photoModeActive,
                whiteboardActive),
            ForegroundSurfaceTransitionKind.ExplicitForeground => ResolveExplicitForeground(
                overlayExists,
                surface),
            _ => ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: false)
        };
    }

    private static SurfaceZOrderDecision ResolveOverlayActivated(
        bool suppressNextApply,
        bool overlayExists,
        bool photoModeActive,
        bool whiteboardActive)
    {
        if (suppressNextApply || !overlayExists)
        {
            return ForegroundSurfaceDecisionFactory.NoTouch(
                requestZOrderApply: !suppressNextApply && overlayExists);
        }

        if (photoModeActive)
        {
            var decision = ForegroundSurfaceDecisionFactory.Touch(ZOrderSurface.PhotoFullscreen);
            return decision with { ForceEnforceZOrder = true };
        }

        if (whiteboardActive)
        {
            var decision = ForegroundSurfaceDecisionFactory.Touch(ZOrderSurface.Whiteboard);
            return decision with { ForceEnforceZOrder = true };
        }

        return ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: true);
    }

    private static SurfaceZOrderDecision ResolveExplicitForeground(
        bool overlayExists,
        ZOrderSurface surface)
    {
        return ForegroundSurfaceDecisionFactory.ExplicitForeground(overlayExists, surface);
    }
}
