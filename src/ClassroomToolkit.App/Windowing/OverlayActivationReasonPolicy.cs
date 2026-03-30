namespace ClassroomToolkit.App.Windowing;

internal static class OverlayActivationReasonPolicy
{
    internal static string ResolveTag(OverlayActivationReason reason)
    {
        return reason switch
        {
            OverlayActivationReason.OverlayHidden => "overlay-hidden",
            OverlayActivationReason.SurfaceNotActivatable => "surface-not-activatable",
            OverlayActivationReason.OverlayAlreadyActive => "overlay-already-active",
            OverlayActivationReason.BlockedByToolbar => "blocked-by-toolbar",
            OverlayActivationReason.BlockedByRollCall => "blocked-by-rollcall",
            OverlayActivationReason.BlockedByImageManager => "blocked-by-image-manager",
            OverlayActivationReason.BlockedByLauncher => "blocked-by-launcher",
            _ => "none"
        };
    }
}
