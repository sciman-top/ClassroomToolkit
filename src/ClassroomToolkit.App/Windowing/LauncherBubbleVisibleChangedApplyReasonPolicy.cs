namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleVisibleChangedApplyReasonPolicy
{
    internal static string ResolveTag(LauncherBubbleVisibleChangedApplyReason reason)
    {
        return reason switch
        {
            LauncherBubbleVisibleChangedApplyReason.BubbleHidden => "bubble-hidden",
            LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed => "visible-changed-suppressed",
            LauncherBubbleVisibleChangedApplyReason.CooldownActive => "cooldown-active",
            _ => "none"
        };
    }
}
