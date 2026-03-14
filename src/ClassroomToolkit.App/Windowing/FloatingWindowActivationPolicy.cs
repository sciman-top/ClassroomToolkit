namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingWindowActivationSnapshot(
    bool OverlayVisible,
    bool OverlayShouldActivate,
    bool OverlayActive,
    bool ImageManagerTopmost,
    bool ImageManagerActive,
    FloatingUtilityActivitySnapshot UtilityActivity);

internal readonly record struct FloatingWindowActivationPlan(
    bool ActivateOverlay,
    bool ActivateImageManager);

internal static class FloatingWindowActivationPolicy
{
    internal static FloatingWindowActivationPlan Resolve(FloatingWindowActivationSnapshot snapshot)
    {
        var overlayDecision = OverlayActivationPolicy.Resolve(
            overlayVisible: snapshot.OverlayVisible,
            overlayShouldActivate: snapshot.OverlayShouldActivate,
            overlayActive: snapshot.OverlayActive,
            toolbarActive: snapshot.UtilityActivity.ToolbarActive,
            imageManagerActive: snapshot.UtilityActivity.ImageManagerActive,
            rollCallActive: snapshot.UtilityActivity.RollCallActive,
            launcherActive: snapshot.UtilityActivity.LauncherActive);
        var imageManagerDecision = ImageManagerActivationPolicy.Resolve(
            imageManagerTopmost: snapshot.ImageManagerTopmost,
            imageManagerActive: snapshot.ImageManagerActive,
            toolbarActive: snapshot.UtilityActivity.ToolbarActive,
            rollCallActive: snapshot.UtilityActivity.RollCallActive,
            launcherActive: snapshot.UtilityActivity.LauncherActive);

        return new FloatingWindowActivationPlan(
            ActivateOverlay: overlayDecision.ShouldActivate,
            ActivateImageManager: imageManagerDecision.ShouldActivate);
    }
}
