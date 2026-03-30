namespace ClassroomToolkit.App.Windowing;

internal static class ImageManagerSurfaceTransitionPolicy
{
    internal static SurfaceZOrderDecision Resolve(
        ImageManagerSurfaceTransitionKind kind,
        bool overlayVisible)
    {
        return kind switch
        {
            ImageManagerSurfaceTransitionKind.Open => ImageManagerSurfaceDecisionFactory.TouchImageManager(
                forceEnforceZOrder: false),
            ImageManagerSurfaceTransitionKind.Activated => ImageManagerSurfaceDecisionFactory.TouchImageManager(
                forceEnforceZOrder: ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerActivated(
                    overlayVisible)),
            ImageManagerSurfaceTransitionKind.Closed => ImageManagerSurfaceDecisionFactory.NoTouch(
                requestZOrderApply: true,
                forceEnforceZOrder: ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerClosed(
                    overlayVisible)),
            ImageManagerSurfaceTransitionKind.StateChanged => ImageManagerSurfaceDecisionFactory.NoTouch(
                requestZOrderApply: overlayVisible,
                forceEnforceZOrder: ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerStateChanged(
                    overlayVisible)),
            _ => ImageManagerSurfaceDecisionFactory.NoTouch(
                requestZOrderApply: false,
                forceEnforceZOrder: false)
        };
    }
}
