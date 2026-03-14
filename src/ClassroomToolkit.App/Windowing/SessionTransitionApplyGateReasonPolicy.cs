namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionApplyGateReasonPolicy
{
    internal static string ResolveTag(SessionTransitionApplyGateReason reason)
    {
        return reason switch
        {
            SessionTransitionApplyGateReason.NoZOrderAction => "no-zorder-action",
            SessionTransitionApplyGateReason.TouchSurfaceRequested => "touch-surface-requested",
            SessionTransitionApplyGateReason.ZOrderApplyRequested => "zorder-apply-requested",
            SessionTransitionApplyGateReason.ForceEnforceRequested => "force-enforce-requested",
            _ => "apply"
        };
    }
}
