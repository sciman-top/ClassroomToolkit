namespace ClassroomToolkit.App.Windowing;

internal static class OverlayNavigationActivateReasonPolicy
{
    internal static string ResolveTag(OverlayNavigationActivateReason reason)
    {
        return reason switch
        {
            OverlayNavigationActivateReason.AvoidActivateRequested => "avoid-activate-requested",
            OverlayNavigationActivateReason.OverlayAlreadyActive => "overlay-already-active",
            OverlayNavigationActivateReason.BlockedByToolbar => "blocked-by-toolbar",
            OverlayNavigationActivateReason.BlockedByRollCall => "blocked-by-rollcall",
            OverlayNavigationActivateReason.BlockedByImageManager => "blocked-by-image-manager",
            OverlayNavigationActivateReason.BlockedByLauncher => "blocked-by-launcher",
            _ => "activate"
        };
    }
}
