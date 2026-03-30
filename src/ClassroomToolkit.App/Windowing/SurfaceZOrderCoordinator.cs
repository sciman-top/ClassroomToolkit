using System;

namespace ClassroomToolkit.App.Windowing;

internal static class SurfaceZOrderCoordinator
{
    internal static bool Apply(
        SurfaceZOrderDecision decision,
        Func<ZOrderSurface, bool> touchSurface,
        Action<bool> requestApply)
    {
        ArgumentNullException.ThrowIfNull(touchSurface);
        ArgumentNullException.ThrowIfNull(requestApply);

        var touchChanged = false;
        if (decision.ShouldTouchSurface)
        {
            touchChanged = touchSurface(decision.Surface);
        }

        var shouldRequestApply = decision.RequestZOrderApply
            && (!decision.ShouldTouchSurface || touchChanged || decision.ForceEnforceZOrder);

        return FloatingZOrderApplyExecutor.Apply(
            shouldRequestApply,
            decision.ForceEnforceZOrder,
            requestApply);
    }
}
