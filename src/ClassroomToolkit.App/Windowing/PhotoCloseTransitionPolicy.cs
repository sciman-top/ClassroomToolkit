namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PhotoCloseTransitionPlan(
    bool SyncFloatingOwnersVisible,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class PhotoCloseTransitionPolicy
{
    internal static PhotoCloseTransitionPlan Resolve(PhotoCloseTransitionContext context)
    {
        return Resolve(context.OverlayVisible);
    }

    internal static PhotoCloseTransitionPlan Resolve(bool overlayVisible)
    {
        return new PhotoCloseTransitionPlan(
            SyncFloatingOwnersVisible: false,
            RequestZOrderApply: overlayVisible,
            ForceEnforceZOrder: overlayVisible);
    }
}
