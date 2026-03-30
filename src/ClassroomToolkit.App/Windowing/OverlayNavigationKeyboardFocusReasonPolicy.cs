namespace ClassroomToolkit.App.Windowing;

internal static class OverlayNavigationKeyboardFocusReasonPolicy
{
    internal static string ResolveTag(OverlayNavigationKeyboardFocusReason reason)
    {
        return reason switch
        {
            OverlayNavigationKeyboardFocusReason.OverlayNotVisible => "overlay-not-visible",
            OverlayNavigationKeyboardFocusReason.BlockedByToolbar => "blocked-by-toolbar",
            OverlayNavigationKeyboardFocusReason.BlockedByRollCall => "blocked-by-rollcall",
            OverlayNavigationKeyboardFocusReason.BlockedByImageManager => "blocked-by-image-manager",
            OverlayNavigationKeyboardFocusReason.BlockedByLauncher => "blocked-by-launcher",
            _ => "focus-keyboard"
        };
    }
}
