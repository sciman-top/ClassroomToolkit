namespace ClassroomToolkit.App.Windowing;

internal static class ImageManagerSurfaceDecisionFactory
{
    internal static SurfaceZOrderDecision TouchImageManager(bool forceEnforceZOrder)
    {
        return new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.ImageManager,
            RequestZOrderApply: true,
            ForceEnforceZOrder: forceEnforceZOrder);
    }

    internal static SurfaceZOrderDecision NoTouch(bool requestZOrderApply, bool forceEnforceZOrder)
    {
        var decision = ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply);
        return decision with { ForceEnforceZOrder = forceEnforceZOrder };
    }
}
