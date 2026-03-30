using System.Collections.Generic;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingWindowZOrderDecision(
    ZOrderSurface FrontSurface,
    FloatingTopmostPlan TopmostPlan,
    bool EnforceZOrder);

internal static class FloatingWindowZOrderDecisionPolicy
{
    public static FloatingWindowZOrderDecision Resolve(
        IWindowOrchestrator windowOrchestrator,
        IList<ZOrderSurface> surfaceStack,
        FloatingWindowRuntimeSnapshot snapshot,
        FloatingTopmostVisibilitySnapshot topmostVisibility,
        ZOrderSurface? lastFrontSurface,
        FloatingTopmostPlan? lastPlan,
        bool forceEnforceZOrder)
    {
        var frontSurface = FloatingFrontSurfaceResolver.Resolve(
            windowOrchestrator,
            surfaceStack,
            snapshot);

        var plan = FloatingTopmostPlanPolicy.Resolve(frontSurface, topmostVisibility);

        var enforceZOrder = FloatingTopmostApplyPolicy.ShouldEnforceZOrder(
            lastFrontSurface,
            frontSurface,
            lastPlan,
            plan,
            forceEnforceZOrder);

        return new FloatingWindowZOrderDecision(
            FrontSurface: frontSurface,
            TopmostPlan: plan,
            EnforceZOrder: enforceZOrder);
    }
}
