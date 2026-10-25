
namespace ClassroomToolkit.App.Windowing;

internal static class OverlayNavigationFocusPolicy
{
    internal static bool ShouldActivateOverlay(
        bool avoidActivate,
        bool overlayActive,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        if (avoidActivate || overlayActive)
        {
            return false;
        }

        return !FloatingWindowCoordinationPolicies
            .ResolveFloatingActivationGuard(utilityActivity)
            .IsBlocked;
    }

    internal static bool ShouldActivateOverlay(
        bool avoidActivate,
        bool overlayActive,
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive)
    {
        return ShouldActivateOverlay(
            avoidActivate,
            overlayActive,
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));
    }

    internal static bool ShouldKeyboardFocusOverlay(
        bool overlayVisible,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        if (!overlayVisible)
        {
            return false;
        }

        return !FloatingWindowCoordinationPolicies
            .ResolveFloatingActivationGuard(utilityActivity)
            .IsBlocked;
    }

    internal static bool ShouldKeyboardFocusOverlay(
        bool overlayVisible,
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive)
    {
        return ShouldKeyboardFocusOverlay(
            overlayVisible,
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));
    }
}

internal readonly record struct OverlayNavigationFocusSnapshot(
    bool OverlayVisible,
    bool OverlayActive,
    FloatingUtilityActivitySnapshot UtilityActivity);
