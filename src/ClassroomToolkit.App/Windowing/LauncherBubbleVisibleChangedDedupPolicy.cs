using System;

namespace ClassroomToolkit.App.Windowing;

internal enum LauncherBubbleVisibleChangedDedupReason
{
    None = 0,
    DuplicateWithinWindow = 1,
    NoHistory = 2,
    DedupDisabledByInterval = 3,
    UnsetTimestamp = 4,
    Applied = 5
}

internal readonly record struct LauncherBubbleVisibleChangedDedupDecision(
    bool ShouldApply,
    LauncherBubbleVisibleChangedDedupReason Reason,
    bool? LastVisibleState,
    DateTime LastEventUtc);

internal static class LauncherBubbleVisibleChangedDedupPolicy
{
    internal static LauncherBubbleVisibleChangedDedupDecision Resolve(
        bool currentVisibleState,
        LauncherBubbleVisibleChangedRuntimeState state,
        DateTime nowUtc,
        int minIntervalMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs)
    {
        return Resolve(
            currentVisibleState,
            state.LastVisibleState,
            state.LastEventUtc,
            nowUtc,
            minIntervalMs);
    }

    internal static LauncherBubbleVisibleChangedDedupDecision Resolve(
        bool currentVisibleState,
        bool? lastVisibleState,
        DateTime lastEventUtc,
        DateTime nowUtc,
        int minIntervalMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs)
    {
        if (!lastVisibleState.HasValue
            || minIntervalMs <= WindowDedupDefaults.MinIntervalMs
            || lastEventUtc == WindowDedupDefaults.UnsetTimestampUtc)
        {
            var reason = !lastVisibleState.HasValue
                ? LauncherBubbleVisibleChangedDedupReason.NoHistory
                : minIntervalMs <= WindowDedupDefaults.MinIntervalMs
                    ? LauncherBubbleVisibleChangedDedupReason.DedupDisabledByInterval
                    : LauncherBubbleVisibleChangedDedupReason.UnsetTimestamp;
            return new LauncherBubbleVisibleChangedDedupDecision(
                ShouldApply: true,
                Reason: reason,
                LastVisibleState: currentVisibleState,
                LastEventUtc: nowUtc);
        }

        var duplicatedState = lastVisibleState.Value == currentVisibleState;
        var withinWindow = (nowUtc - lastEventUtc).TotalMilliseconds < minIntervalMs;
        if (duplicatedState && withinWindow)
        {
            return new LauncherBubbleVisibleChangedDedupDecision(
                ShouldApply: false,
                Reason: LauncherBubbleVisibleChangedDedupReason.DuplicateWithinWindow,
                LastVisibleState: lastVisibleState,
                LastEventUtc: lastEventUtc);
        }

        return new LauncherBubbleVisibleChangedDedupDecision(
            ShouldApply: true,
            Reason: LauncherBubbleVisibleChangedDedupReason.Applied,
            LastVisibleState: currentVisibleState,
            LastEventUtc: nowUtc);
    }
}
