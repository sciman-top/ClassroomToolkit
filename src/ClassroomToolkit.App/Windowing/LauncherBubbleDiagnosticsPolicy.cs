namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleDiagnosticsPolicy
{
    internal static string FormatVisibleChangedGateSkipMessage(
        LauncherBubbleZOrderApplyGateReason reason,
        LauncherBubbleVisibleChangedApplyReason sourceReason = LauncherBubbleVisibleChangedApplyReason.None)
    {
        var message = $"[LauncherBubble][VisibleChangedGate] skip reason={LauncherBubbleZOrderApplyGateReasonPolicy.ResolveTag(reason)}";
        if (sourceReason != LauncherBubbleVisibleChangedApplyReason.None)
        {
            message += $" source={LauncherBubbleVisibleChangedApplyReasonPolicy.ResolveTag(sourceReason)}";
        }

        return message;
    }

    internal static string FormatVisibleChangedDedupSkipMessage(LauncherBubbleVisibleChangedDedupReason reason)
    {
        return $"[LauncherBubble][VisibleChangedDedup] skip reason={LauncherBubbleVisibleChangedDedupReasonPolicy.ResolveTag(reason)}";
    }
}
