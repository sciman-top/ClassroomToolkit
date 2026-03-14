namespace ClassroomToolkit.App.Windowing;

internal static class LauncherVisibilityTransitionReasonPolicy
{
    internal static string ResolveMinimizeTag(LauncherVisibilityMinimizeReason reason)
    {
        return reason switch
        {
            LauncherVisibilityMinimizeReason.HideMainAndShowBubble => "hide-main-and-show-bubble",
            LauncherVisibilityMinimizeReason.HideMainOnly => "hide-main-only",
            LauncherVisibilityMinimizeReason.ShowBubbleOnly => "show-bubble-only",
            LauncherVisibilityMinimizeReason.NoOp => "no-op",
            _ => "none"
        };
    }

    internal static string ResolveRestoreTag(LauncherVisibilityRestoreReason reason)
    {
        return reason switch
        {
            LauncherVisibilityRestoreReason.ShowMainAndHideBubble => "show-main-and-hide-bubble",
            LauncherVisibilityRestoreReason.ShowMainOnly => "show-main-only",
            LauncherVisibilityRestoreReason.HideBubbleOnly => "hide-bubble-only",
            LauncherVisibilityRestoreReason.NoOp => "no-op",
            _ => "none"
        };
    }
}
