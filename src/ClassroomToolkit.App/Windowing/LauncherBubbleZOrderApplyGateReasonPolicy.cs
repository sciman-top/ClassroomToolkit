namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleZOrderApplyGateReasonPolicy
{
    internal static string ResolveTag(LauncherBubbleZOrderApplyGateReason reason)
    {
        return reason switch
        {
            LauncherBubbleZOrderApplyGateReason.AppClosing => "app-closing",
            LauncherBubbleZOrderApplyGateReason.BubbleWindowMissing => "bubble-missing",
            LauncherBubbleZOrderApplyGateReason.BubbleHidden => "bubble-hidden",
            LauncherBubbleZOrderApplyGateReason.VisibleChangedSuppressed => "visible-changed-suppressed",
            LauncherBubbleZOrderApplyGateReason.CooldownActive => "cooldown-active",
            _ => "apply"
        };
    }
}
