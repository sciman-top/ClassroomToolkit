namespace ClassroomToolkit.App.Windowing;

internal static class FloatingActivationGuardReasonPolicy
{
    internal static string ResolveTag(FloatingActivationGuardReason reason)
    {
        return reason switch
        {
            FloatingActivationGuardReason.ToolbarActive => "toolbar-active",
            FloatingActivationGuardReason.RollCallActive => "rollcall-active",
            FloatingActivationGuardReason.ImageManagerActive => "image-manager-active",
            FloatingActivationGuardReason.LauncherActive => "launcher-active",
            _ => "none"
        };
    }
}
