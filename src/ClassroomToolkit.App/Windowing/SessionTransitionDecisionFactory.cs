namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionDecisionFactory
{
    internal static SurfaceZOrderDecision Create(
        SessionTransitionSurfaceDecision surfaceDecision,
        SessionTransitionApplyDecision applyDecision)
    {
        var decision = surfaceDecision.ShouldTouchSurface
            ? ForegroundSurfaceDecisionFactory.Touch(surfaceDecision.Surface)
            : ForegroundSurfaceDecisionFactory.NoTouch(applyDecision.RequestZOrderApply);

        return decision with
        {
            RequestZOrderApply = applyDecision.RequestZOrderApply,
            ForceEnforceZOrder = applyDecision.ForceEnforceZOrder
        };
    }
}
