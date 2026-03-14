namespace ClassroomToolkit.App.Windowing;

internal static class ImageManagerVisibilitySurfaceDecisionPolicy
{
    internal static SurfaceZOrderDecision ResolveOpen(ImageManagerVisibilityTransitionPlan plan)
    {
        if (plan.TouchImageManagerSurface)
        {
            return ImageManagerSurfaceDecisionFactory.TouchImageManager(
                forceEnforceZOrder: plan.ForceEnforceZOrder);
        }

        return ImageManagerSurfaceDecisionFactory.NoTouch(
            requestZOrderApply: plan.RequestZOrderApply,
            forceEnforceZOrder: plan.ForceEnforceZOrder);
    }
}
