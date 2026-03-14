namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingTopmostPlan(
    bool ToolbarTopmost,
    bool RollCallTopmost,
    bool LauncherTopmost,
    bool ImageManagerTopmost,
    bool OverlayShouldActivate);

internal static class FloatingTopmostPlanPolicy
{
    internal static FloatingTopmostPlan Resolve(
        ZOrderSurface frontSurface,
        FloatingTopmostVisibilitySnapshot snapshot)
    {
        return Resolve(
            frontSurface,
            snapshot.ToolbarVisible,
            snapshot.RollCallVisible,
            snapshot.LauncherVisible,
            snapshot.ImageManagerVisible,
            snapshot.OverlayVisible);
    }

    internal static FloatingTopmostPlan Resolve(
        ZOrderSurface frontSurface,
        bool toolbarVisible,
        bool rollCallVisible,
        bool launcherVisible,
        bool imageManagerVisible,
        bool overlayVisible)
    {
        var imageManagerTopmostDecision = ImageManagerTopmostPolicy.Resolve(imageManagerVisible, frontSurface);
        var overlayActivationSurfaceDecision = OverlayActivationSurfacePolicy.Resolve(overlayVisible, frontSurface);

        return new FloatingTopmostPlan(
            ToolbarTopmost: toolbarVisible,
            RollCallTopmost: rollCallVisible,
            LauncherTopmost: launcherVisible,
            ImageManagerTopmost: imageManagerTopmostDecision.ShouldApply,
            OverlayShouldActivate: overlayActivationSurfaceDecision.ShouldActivate);
    }
}
