using System;

namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleVisibleChangedStateUpdater
{
    internal static void Apply(
        ref LauncherBubbleVisibleChangedRuntimeState state,
        LauncherBubbleVisibleChangedDedupDecision decision)
    {
        state = new LauncherBubbleVisibleChangedRuntimeState(
            LastVisibleState: decision.LastVisibleState,
            LastEventUtc: decision.LastEventUtc);
    }

    internal static void Apply(
        ref bool? lastVisibleState,
        ref DateTime lastEventUtc,
        LauncherBubbleVisibleChangedDedupDecision decision)
    {
        lastVisibleState = decision.LastVisibleState;
        lastEventUtc = decision.LastEventUtc;
    }
}
