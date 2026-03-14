namespace ClassroomToolkit.App.Windowing;

internal enum SessionTransitionApplyGateReason
{
    None = 0,
    NoZOrderAction = 1,
    TouchSurfaceRequested = 2,
    ZOrderApplyRequested = 3,
    ForceEnforceRequested = 4
}

internal readonly record struct SessionTransitionApplyGateDecision(
    bool ShouldApply,
    SessionTransitionApplyGateReason Reason);

internal static class SessionTransitionApplyGatePolicy
{
    internal static SessionTransitionApplyGateDecision Resolve(SurfaceZOrderDecision decision)
    {
        if (decision.ShouldTouchSurface)
        {
            return new SessionTransitionApplyGateDecision(
                ShouldApply: true,
                Reason: SessionTransitionApplyGateReason.TouchSurfaceRequested);
        }

        if (decision.RequestZOrderApply)
        {
            return new SessionTransitionApplyGateDecision(
                ShouldApply: true,
                Reason: SessionTransitionApplyGateReason.ZOrderApplyRequested);
        }

        if (decision.ForceEnforceZOrder)
        {
            return new SessionTransitionApplyGateDecision(
                ShouldApply: true,
                Reason: SessionTransitionApplyGateReason.ForceEnforceRequested);
        }

        return new SessionTransitionApplyGateDecision(
            ShouldApply: false,
            Reason: SessionTransitionApplyGateReason.NoZOrderAction);
    }

    internal static bool ShouldApply(SurfaceZOrderDecision decision)
    {
        return Resolve(decision).ShouldApply;
    }
}
