using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePendingTakeoverPolicy
{
    internal const int ImmediateTakeoverThresholdMs = 120;

    internal static CrossPageDisplayUpdateDispatchDecision Resolve(
        CrossPageDisplayUpdateDispatchDecision decision,
        CrossPageUpdateDispatchSuffix suffix,
        CrossPageDisplayUpdateRuntimeState pendingState,
        DateTime nowUtc,
        int thresholdMs = ImmediateTakeoverThresholdMs)
    {
        if (decision.Mode != CrossPageDisplayUpdateDispatchMode.SkipPending)
        {
            return decision;
        }

        if (suffix != CrossPageUpdateDispatchSuffix.Immediate)
        {
            return decision;
        }

        if (!pendingState.Pending || pendingState.PendingSinceUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return decision;
        }

        var pendingMs = (nowUtc - pendingState.PendingSinceUtc).TotalMilliseconds;
        if (pendingMs < thresholdMs)
        {
            return decision;
        }

        return new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.Direct,
            DelayMs: 0);
    }
}
