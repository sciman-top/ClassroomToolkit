namespace ClassroomToolkit.App.Windowing;

internal enum OverlayActivationReason
{
    None = 0,
    OverlayHidden = 1,
    SurfaceNotActivatable = 2,
    OverlayAlreadyActive = 3,
    BlockedByToolbar = 4,
    BlockedByRollCall = 5,
    BlockedByImageManager = 6,
    BlockedByLauncher = 7
}

internal readonly record struct OverlayActivationDecision(
    bool ShouldActivate,
    OverlayActivationReason Reason);

internal static class OverlayActivationPolicy
{
    internal static OverlayActivationDecision Resolve(
        bool overlayVisible,
        bool overlayShouldActivate,
        bool overlayActive,
        bool toolbarActive,
        bool imageManagerActive,
        bool rollCallActive,
        bool launcherActive)
    {
        if (!overlayVisible)
        {
            return new OverlayActivationDecision(
                ShouldActivate: false,
                Reason: OverlayActivationReason.OverlayHidden);
        }

        if (!overlayShouldActivate)
        {
            return new OverlayActivationDecision(
                ShouldActivate: false,
                Reason: OverlayActivationReason.SurfaceNotActivatable);
        }

        if (overlayActive)
        {
            return new OverlayActivationDecision(
                ShouldActivate: false,
                Reason: OverlayActivationReason.OverlayAlreadyActive);
        }

        var guardDecision = FloatingActivationGuardPolicy.Resolve(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));
        return guardDecision.IsBlocked
            ? new OverlayActivationDecision(
                ShouldActivate: false,
                Reason: guardDecision.Reason switch
                {
                    FloatingActivationGuardReason.ToolbarActive => OverlayActivationReason.BlockedByToolbar,
                    FloatingActivationGuardReason.RollCallActive => OverlayActivationReason.BlockedByRollCall,
                    FloatingActivationGuardReason.ImageManagerActive => OverlayActivationReason.BlockedByImageManager,
                    FloatingActivationGuardReason.LauncherActive => OverlayActivationReason.BlockedByLauncher,
                    _ => OverlayActivationReason.BlockedByToolbar
                })
            : new OverlayActivationDecision(
                ShouldActivate: true,
                Reason: OverlayActivationReason.None);
    }

    internal static bool ShouldActivate(
        bool overlayVisible,
        bool overlayShouldActivate,
        bool overlayActive,
        bool toolbarActive,
        bool imageManagerActive,
        bool rollCallActive,
        bool launcherActive)
    {
        return Resolve(
            overlayVisible,
            overlayShouldActivate,
            overlayActive,
            toolbarActive: toolbarActive,
            imageManagerActive: imageManagerActive,
            rollCallActive: rollCallActive,
            launcherActive: launcherActive).ShouldActivate;
    }
}
