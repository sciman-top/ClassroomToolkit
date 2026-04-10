namespace ClassroomToolkit.App.Windowing;

internal static partial class OverlayNavigationFocusPolicy
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
                Reason: MapBlockedActivateReason(guardDecision.Reason))
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
                Reason: MapBlockedKeyboardFocusReason(guardDecision.Reason))
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
