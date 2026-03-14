namespace ClassroomToolkit.App.Windowing;

internal static class ForegroundSurfaceDecisionFactory
{
    internal static SurfaceZOrderDecision NoTouch(bool requestZOrderApply)
    {
        return new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: requestZOrderApply,
            ForceEnforceZOrder: false);
    }

    internal static SurfaceZOrderDecision Touch(ZOrderSurface surface)
    {
        return new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: surface,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);
    }

    internal static SurfaceZOrderDecision ExplicitForeground(bool overlayExists, ZOrderSurface surface)
    {
        return new SurfaceZOrderDecision(
            ShouldTouchSurface: overlayExists && surface != ZOrderSurface.None,
            Surface: overlayExists ? surface : ZOrderSurface.None,
            RequestZOrderApply: overlayExists,
            ForceEnforceZOrder: overlayExists);
    }
}
