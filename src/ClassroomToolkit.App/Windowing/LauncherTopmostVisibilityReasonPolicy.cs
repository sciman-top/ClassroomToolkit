namespace ClassroomToolkit.App.Windowing;

internal static class LauncherTopmostVisibilityReasonPolicy
{
    internal static string ResolveTag(LauncherTopmostVisibilityReason reason)
    {
        return reason switch
        {
            LauncherTopmostVisibilityReason.MainVisible => "main-visible",
            LauncherTopmostVisibilityReason.MainHiddenOrMinimized => "main-hidden-or-minimized",
            LauncherTopmostVisibilityReason.BubbleVisible => "bubble-visible",
            LauncherTopmostVisibilityReason.BubbleHiddenOrMinimized => "bubble-hidden-or-minimized",
            _ => "none"
        };
    }
}
