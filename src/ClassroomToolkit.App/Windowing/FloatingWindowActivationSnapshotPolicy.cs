namespace ClassroomToolkit.App.Windowing;

internal static class FloatingWindowActivationSnapshotPolicy
{
    public static FloatingWindowActivationSnapshot Resolve(
        FloatingWindowRuntimeSnapshot runtimeSnapshot,
        FloatingTopmostPlan topmostPlan,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        return new FloatingWindowActivationSnapshot(
            OverlayVisible: runtimeSnapshot.OverlayVisible,
            OverlayShouldActivate: topmostPlan.OverlayShouldActivate,
            OverlayActive: runtimeSnapshot.OverlayActive,
            ImageManagerTopmost: topmostPlan.ImageManagerTopmost,
            ImageManagerActive: utilityActivity.ImageManagerActive,
            UtilityActivity: utilityActivity);
    }
}
