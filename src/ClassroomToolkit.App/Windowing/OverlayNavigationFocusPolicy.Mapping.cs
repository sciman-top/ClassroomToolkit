namespace ClassroomToolkit.App.Windowing;

internal static partial class OverlayNavigationFocusPolicy
{
    private static OverlayNavigationActivateReason MapBlockedActivateReason(
        FloatingActivationGuardReason reason)
    {
        return reason switch
        {
            FloatingActivationGuardReason.ToolbarActive => OverlayNavigationActivateReason.BlockedByToolbar,
            FloatingActivationGuardReason.RollCallActive => OverlayNavigationActivateReason.BlockedByRollCall,
            FloatingActivationGuardReason.ImageManagerActive => OverlayNavigationActivateReason.BlockedByImageManager,
            FloatingActivationGuardReason.LauncherActive => OverlayNavigationActivateReason.BlockedByLauncher,
            _ => OverlayNavigationActivateReason.BlockedByToolbar
        };
    }

    private static OverlayNavigationKeyboardFocusReason MapBlockedKeyboardFocusReason(
        FloatingActivationGuardReason reason)
    {
        return reason switch
        {
            FloatingActivationGuardReason.ToolbarActive => OverlayNavigationKeyboardFocusReason.BlockedByToolbar,
            FloatingActivationGuardReason.RollCallActive => OverlayNavigationKeyboardFocusReason.BlockedByRollCall,
            FloatingActivationGuardReason.ImageManagerActive => OverlayNavigationKeyboardFocusReason.BlockedByImageManager,
            FloatingActivationGuardReason.LauncherActive => OverlayNavigationKeyboardFocusReason.BlockedByLauncher,
            _ => OverlayNavigationKeyboardFocusReason.BlockedByToolbar
        };
    }
}
