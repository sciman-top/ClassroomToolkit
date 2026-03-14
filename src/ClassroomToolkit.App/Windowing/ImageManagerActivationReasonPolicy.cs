namespace ClassroomToolkit.App.Windowing;

internal static class ImageManagerActivationReasonPolicy
{
    internal static string ResolveTag(ImageManagerActivationReason reason)
    {
        return reason switch
        {
            ImageManagerActivationReason.NotTopmostTarget => "not-topmost-target",
            ImageManagerActivationReason.AlreadyActive => "already-active",
            ImageManagerActivationReason.BlockedByToolbar => "blocked-by-toolbar",
            ImageManagerActivationReason.BlockedByRollCall => "blocked-by-rollcall",
            ImageManagerActivationReason.BlockedByLauncher => "blocked-by-launcher",
            _ => "none"
        };
    }
}
