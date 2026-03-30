namespace ClassroomToolkit.App.Windowing;

internal static class ImageManagerStateChangeSurfaceDecisionPolicy
{
    internal static SurfaceZOrderDecision Resolve(ImageManagerStateChangeDecision decision)
    {
        return ImageManagerSurfaceDecisionFactory.NoTouch(
            requestZOrderApply: decision.RequestZOrderApply,
            forceEnforceZOrder: decision.ForceEnforceZOrder);
    }
}
