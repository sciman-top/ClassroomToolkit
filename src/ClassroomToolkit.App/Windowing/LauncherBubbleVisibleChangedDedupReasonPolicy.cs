namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleVisibleChangedDedupReasonPolicy
{
    internal static string ResolveTag(LauncherBubbleVisibleChangedDedupReason reason)
    {
        return reason switch
        {
            LauncherBubbleVisibleChangedDedupReason.DuplicateWithinWindow => "duplicate-within-window",
            LauncherBubbleVisibleChangedDedupReason.NoHistory => "no-history",
            LauncherBubbleVisibleChangedDedupReason.DedupDisabledByInterval => "interval-disabled",
            LauncherBubbleVisibleChangedDedupReason.UnsetTimestamp => "unset-timestamp",
            LauncherBubbleVisibleChangedDedupReason.Applied => "applied",
            _ => "none"
        };
    }
}
