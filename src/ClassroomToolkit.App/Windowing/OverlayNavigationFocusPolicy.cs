namespace ClassroomToolkit.App.Windowing;

internal readonly record struct OverlayNavigationFocusPlan(
    bool ActivateOverlay,
    bool KeyboardFocusOverlay);

internal readonly record struct OverlayNavigationFocusPlanDecision(
    OverlayNavigationFocusPlan Plan,
    OverlayNavigationActivateReason ActivateReason,
    OverlayNavigationKeyboardFocusReason KeyboardFocusReason);

internal enum OverlayNavigationActivateReason
{
    None = 0,
    AvoidActivateRequested = 1,
    OverlayAlreadyActive = 2,
    BlockedByToolbar = 3,
    BlockedByRollCall = 4,
    BlockedByImageManager = 5,
    BlockedByLauncher = 6
}

internal readonly record struct OverlayNavigationActivateDecision(
    bool ShouldActivateOverlay,
    OverlayNavigationActivateReason Reason);

internal enum OverlayNavigationKeyboardFocusReason
{
    None = 0,
    OverlayNotVisible = 1,
    BlockedByToolbar = 2,
    BlockedByRollCall = 3,
    BlockedByImageManager = 4,
    BlockedByLauncher = 5
}

internal readonly record struct OverlayNavigationKeyboardFocusDecision(
    bool ShouldKeyboardFocusOverlay,
    OverlayNavigationKeyboardFocusReason Reason);

internal static class OverlayNavigationFocusPolicy
{
    internal static OverlayNavigationFocusPlanDecision ResolvePlanDecision(
        bool avoidActivate,
        OverlayNavigationFocusSnapshot snapshot)
    {
        var activateDecision = ResolveActivateDecision(
            avoidActivate,
            snapshot.OverlayActive,
            snapshot.UtilityActivity);
        var keyboardFocusDecision = ResolveKeyboardFocusDecision(
            snapshot.OverlayVisible,
            snapshot.UtilityActivity);
        return new OverlayNavigationFocusPlanDecision(
            Plan: new OverlayNavigationFocusPlan(
                ActivateOverlay: activateDecision.ShouldActivateOverlay,
                KeyboardFocusOverlay: keyboardFocusDecision.ShouldKeyboardFocusOverlay),
            ActivateReason: activateDecision.Reason,
            KeyboardFocusReason: keyboardFocusDecision.Reason);
    }

    internal static OverlayNavigationFocusPlan ResolvePlan(
        bool avoidActivate,
        OverlayNavigationFocusSnapshot snapshot)
    {
        return ResolvePlanDecision(avoidActivate, snapshot).Plan;
    }

    internal static OverlayNavigationFocusPlan ResolvePlan(
        bool avoidActivate,
        bool overlayVisible,
        bool overlayActive,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        return ResolvePlan(
            avoidActivate,
            OverlayNavigationFocusSnapshotPolicy.Resolve(
                overlayVisible,
                overlayActive,
                utilityActivity));
    }

    internal static bool ShouldActivateOverlay(
        bool avoidActivate,
        bool overlayActive,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        return ResolveActivateDecision(
            avoidActivate,
            overlayActive,
            utilityActivity).ShouldActivateOverlay;
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
        return ResolveKeyboardFocusDecision(
            overlayVisible,
            utilityActivity).ShouldKeyboardFocusOverlay;
    }

    internal static OverlayNavigationActivateDecision ResolveActivateDecision(
        bool avoidActivate,
        bool overlayActive,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        if (avoidActivate)
        {
            return new OverlayNavigationActivateDecision(
                ShouldActivateOverlay: false,
                Reason: OverlayNavigationActivateReason.AvoidActivateRequested);
        }

        if (overlayActive)
        {
            return new OverlayNavigationActivateDecision(
                ShouldActivateOverlay: false,
                Reason: OverlayNavigationActivateReason.OverlayAlreadyActive);
        }

        var guardDecision = FloatingActivationGuardPolicy.Resolve(utilityActivity);
        return guardDecision.IsBlocked
            ? new OverlayNavigationActivateDecision(
                ShouldActivateOverlay: false,
                Reason: guardDecision.Reason switch
                {
                    FloatingActivationGuardReason.ToolbarActive => OverlayNavigationActivateReason.BlockedByToolbar,
                    FloatingActivationGuardReason.RollCallActive => OverlayNavigationActivateReason.BlockedByRollCall,
                    FloatingActivationGuardReason.ImageManagerActive => OverlayNavigationActivateReason.BlockedByImageManager,
                    FloatingActivationGuardReason.LauncherActive => OverlayNavigationActivateReason.BlockedByLauncher,
                    _ => OverlayNavigationActivateReason.BlockedByToolbar
                })
            : new OverlayNavigationActivateDecision(
                ShouldActivateOverlay: true,
                Reason: OverlayNavigationActivateReason.None);
    }

    internal static OverlayNavigationKeyboardFocusDecision ResolveKeyboardFocusDecision(
        bool overlayVisible,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        if (!overlayVisible)
        {
            return new OverlayNavigationKeyboardFocusDecision(
                ShouldKeyboardFocusOverlay: false,
                Reason: OverlayNavigationKeyboardFocusReason.OverlayNotVisible);
        }

        var guardDecision = FloatingActivationGuardPolicy.Resolve(utilityActivity);
        return guardDecision.IsBlocked
            ? new OverlayNavigationKeyboardFocusDecision(
                ShouldKeyboardFocusOverlay: false,
                Reason: guardDecision.Reason switch
                {
                    FloatingActivationGuardReason.ToolbarActive => OverlayNavigationKeyboardFocusReason.BlockedByToolbar,
                    FloatingActivationGuardReason.RollCallActive => OverlayNavigationKeyboardFocusReason.BlockedByRollCall,
                    FloatingActivationGuardReason.ImageManagerActive => OverlayNavigationKeyboardFocusReason.BlockedByImageManager,
                    FloatingActivationGuardReason.LauncherActive => OverlayNavigationKeyboardFocusReason.BlockedByLauncher,
                    _ => OverlayNavigationKeyboardFocusReason.BlockedByToolbar
                })
            : new OverlayNavigationKeyboardFocusDecision(
                ShouldKeyboardFocusOverlay: true,
                Reason: OverlayNavigationKeyboardFocusReason.None);
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
