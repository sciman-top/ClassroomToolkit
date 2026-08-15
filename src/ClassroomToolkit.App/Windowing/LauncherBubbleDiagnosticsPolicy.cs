namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleDiagnosticsPolicy
{
    internal static string FormatVisibleChangedGateSkipMessage(
        LauncherBubbleZOrderApplyGateReason reason,
        LauncherBubbleVisibleChangedApplyReason sourceReason = LauncherBubbleVisibleChangedApplyReason.None)
    {
        var message = $"[LauncherBubble][VisibleChangedGate] skip reason={reason}";
        if (sourceReason != LauncherBubbleVisibleChangedApplyReason.None)
        {
            message += $" source={sourceReason}";
        }

        return message;
    }

    internal static string FormatVisibleChangedDedupSkipMessage(LauncherBubbleVisibleChangedDedupReason reason)
    {
        return $"[LauncherBubble][VisibleChangedDedup] skip reason={reason}";
    }
}
