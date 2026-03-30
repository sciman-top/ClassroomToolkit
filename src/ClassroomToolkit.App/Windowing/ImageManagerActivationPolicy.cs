namespace ClassroomToolkit.App.Windowing;

internal enum ImageManagerActivationReason
{
    None = 0,
    NotTopmostTarget = 1,
    AlreadyActive = 2,
    BlockedByToolbar = 3,
    BlockedByRollCall = 4,
    BlockedByLauncher = 5
}

internal readonly record struct ImageManagerActivationDecision(
    bool ShouldActivate,
    ImageManagerActivationReason Reason);

internal static class ImageManagerActivationPolicy
{
    internal static ImageManagerActivationDecision Resolve(
        bool imageManagerTopmost,
        bool imageManagerActive,
        bool toolbarActive,
        bool rollCallActive,
        bool launcherActive)
    {
        if (!imageManagerTopmost)
        {
            return new ImageManagerActivationDecision(
                ShouldActivate: false,
                Reason: ImageManagerActivationReason.NotTopmostTarget);
        }

        if (imageManagerActive)
        {
            return new ImageManagerActivationDecision(
                ShouldActivate: false,
                Reason: ImageManagerActivationReason.AlreadyActive);
        }

        var guardDecision = FloatingActivationGuardPolicy.Resolve(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: false,
                LauncherActive: launcherActive));
        return guardDecision.IsBlocked
            ? new ImageManagerActivationDecision(
                ShouldActivate: false,
                Reason: guardDecision.Reason switch
                {
                    FloatingActivationGuardReason.ToolbarActive => ImageManagerActivationReason.BlockedByToolbar,
                    FloatingActivationGuardReason.RollCallActive => ImageManagerActivationReason.BlockedByRollCall,
                    FloatingActivationGuardReason.LauncherActive => ImageManagerActivationReason.BlockedByLauncher,
                    _ => ImageManagerActivationReason.BlockedByToolbar
                })
            : new ImageManagerActivationDecision(
                ShouldActivate: true,
                Reason: ImageManagerActivationReason.None);
    }

    internal static bool ShouldActivate(
        bool imageManagerTopmost,
        bool imageManagerActive,
        bool toolbarActive,
        bool rollCallActive,
        bool launcherActive)
    {
        return Resolve(
            imageManagerTopmost,
            imageManagerActive,
            toolbarActive,
            rollCallActive,
            launcherActive).ShouldActivate;
    }
}
