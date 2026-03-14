using System;

namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleVisibilityStateUpdater
{
    internal static void MarkSuppressVisibleChangedApply(
        ref LauncherBubbleVisibilityRuntimeState state,
        bool suppress)
    {
        state = state with { SuppressVisibleChangedApply = suppress };
    }

    internal static void MarkVisibleChangedSuppressionCooldown(
        ref LauncherBubbleVisibilityRuntimeState state,
        DateTime nowUtc,
        int cooldownMs = LauncherBubbleVisibleChangedSuppressionDefaults.TransitionCooldownMs)
    {
        var untilUtc = cooldownMs <= 0
            ? WindowDedupDefaults.UnsetTimestampUtc
            : nowUtc.AddMilliseconds(cooldownMs);
        state = state with { SuppressVisibleChangedUntilUtc = untilUtc };
    }

    internal static void ApplyVisibleChangedDecision(
        ref LauncherBubbleVisibilityRuntimeState state,
        LauncherBubbleVisibleChangedDedupDecision decision)
    {
        state = state with
        {
            VisibleChangedState = new LauncherBubbleVisibleChangedRuntimeState(
                LastVisibleState: decision.LastVisibleState,
                LastEventUtc: decision.LastEventUtc)
        };
    }
}
