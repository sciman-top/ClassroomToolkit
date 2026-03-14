namespace ClassroomToolkit.App.Windowing;

internal readonly record struct RollCallVisibilityTransitionPlan(
    bool SyncOwnerToOverlay,
    bool ShowWindow,
    bool HideWindow,
    bool ActivateWindow,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class RollCallVisibilityTransitionPolicy
{
    internal static RollCallVisibilityTransitionPlan Resolve(RollCallVisibilityTransitionContext context)
    {
        return Resolve(
            rollCallVisible: context.RollCallVisible,
            rollCallActive: context.RollCallActive,
            overlayVisible: context.OverlayVisible);
    }

    internal static RollCallVisibilityTransitionPlan Resolve(
        bool rollCallVisible,
        bool rollCallActive,
        bool overlayVisible)
    {
        if (rollCallVisible)
        {
            return new RollCallVisibilityTransitionPlan(
                SyncOwnerToOverlay: false,
                ShowWindow: false,
                HideWindow: true,
                ActivateWindow: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: overlayVisible);
        }
        var activateWindowDecision = UserInitiatedWindowActivationPolicy.Resolve(
            windowVisible: true,
            windowActive: rollCallActive);

        return new RollCallVisibilityTransitionPlan(
            SyncOwnerToOverlay: overlayVisible,
            ShowWindow: true,
            HideWindow: false,
            ActivateWindow: activateWindowDecision.ShouldActivateAfterShow,
            RequestZOrderApply: true,
            ForceEnforceZOrder: overlayVisible);
    }
}
