namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostApplyPolicy
{
    private readonly record struct TopmostPlanSnapshot(
        bool ToolbarTopmost,
        bool RollCallTopmost,
        bool LauncherTopmost,
        bool ImageManagerTopmost);

    internal enum FloatingTopmostApplyReason
    {
        None = 0,
        ForceRequested = 1,
        MissingLastState = 2,
        FrontSurfaceChanged = 3,
        TopmostPlanChanged = 4,
        Unchanged = 5,
        LauncherInteractiveRetouch = 6
    }

    internal readonly record struct FloatingTopmostApplyDecision(
        bool ShouldEnforceZOrder,
        FloatingTopmostApplyReason Reason);

    internal static FloatingTopmostApplyDecision Resolve(
        ZOrderSurface? lastFrontSurface,
        ZOrderSurface currentFrontSurface,
        FloatingTopmostPlan? lastPlan,
        FloatingTopmostPlan currentPlan,
        bool forceEnforceZOrder = false)
    {
        if (forceEnforceZOrder)
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.ForceRequested);
        }

        if (!lastFrontSurface.HasValue || !lastPlan.HasValue)
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.MissingLastState);
        }

        if (lastFrontSurface.Value != currentFrontSurface)
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.FrontSurfaceChanged);
        }

        var lastTopmost = ToTopmostPlan(lastPlan.Value);
        var currentTopmost = ToTopmostPlan(currentPlan);
        if (lastTopmost != currentTopmost)
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.TopmostPlanChanged);
        }

        if (ShouldRetouchLauncherOnInteractiveSurface(currentFrontSurface, currentPlan))
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.LauncherInteractiveRetouch);
        }

        return new FloatingTopmostApplyDecision(
            ShouldEnforceZOrder: false,
            Reason: FloatingTopmostApplyReason.Unchanged);
    }

    internal static bool ShouldEnforceZOrder(
        ZOrderSurface? lastFrontSurface,
        ZOrderSurface currentFrontSurface,
        FloatingTopmostPlan? lastPlan,
        FloatingTopmostPlan currentPlan,
        bool forceEnforceZOrder = false)
    {
        return Resolve(
            lastFrontSurface,
            currentFrontSurface,
            lastPlan,
            currentPlan,
            forceEnforceZOrder).ShouldEnforceZOrder;
    }

    private static TopmostPlanSnapshot ToTopmostPlan(FloatingTopmostPlan plan)
        => new(
            plan.ToolbarTopmost,
            plan.RollCallTopmost,
            plan.LauncherTopmost,
            plan.ImageManagerTopmost);

    private static bool ShouldRetouchLauncherOnInteractiveSurface(
        ZOrderSurface frontSurface,
        FloatingTopmostPlan plan)
    {
        if (!plan.LauncherTopmost)
        {
            return false;
        }

        // Photo mode already has dedicated foreground/watchdog retouch gating; forcing
        // launcher retouch on every photo z-order request can cause visible flicker
        // when floating utility windows overlap the photo content.
        return frontSurface is ZOrderSurface.PresentationFullscreen
            or ZOrderSurface.Whiteboard;
    }
}
