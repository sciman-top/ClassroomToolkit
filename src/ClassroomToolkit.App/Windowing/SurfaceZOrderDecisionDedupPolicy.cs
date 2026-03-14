using System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct SurfaceZOrderDecisionDedupDecision(
    bool ShouldApply,
    SurfaceZOrderDecision LastDecision,
    DateTime LastAppliedUtc,
    SurfaceZOrderDecisionDedupReason Reason);

internal enum SurfaceZOrderDecisionDedupReason
{
    None = 0,
    NoHistory = 1,
    DedupDisabledByInterval = 2,
    UnsetTimestamp = 3,
    SkippedWithinDedupWindow = 4,
    Applied = 5
}

internal static class SurfaceZOrderDecisionDedupPolicy
{
    internal static SurfaceZOrderDecisionDedupDecision Resolve(
        SurfaceZOrderDecision currentDecision,
        SurfaceZOrderDecisionRuntimeState state,
        DateTime nowUtc,
        int minIntervalMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs)
    {
        return Resolve(
            currentDecision,
            state.LastDecision,
            state.LastAppliedUtc,
            nowUtc,
            minIntervalMs);
    }

    internal static SurfaceZOrderDecisionDedupDecision Resolve(
        SurfaceZOrderDecision currentDecision,
        SurfaceZOrderDecision? lastDecision,
        DateTime lastAppliedUtc,
        DateTime nowUtc,
        int minIntervalMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs)
    {
        if (!lastDecision.HasValue
            || minIntervalMs <= WindowDedupDefaults.MinIntervalMs
            || lastAppliedUtc == WindowDedupDefaults.UnsetTimestampUtc)
        {
            var reason = !lastDecision.HasValue
                ? SurfaceZOrderDecisionDedupReason.NoHistory
                : minIntervalMs <= WindowDedupDefaults.MinIntervalMs
                    ? SurfaceZOrderDecisionDedupReason.DedupDisabledByInterval
                    : SurfaceZOrderDecisionDedupReason.UnsetTimestamp;
            return new SurfaceZOrderDecisionDedupDecision(
                ShouldApply: true,
                LastDecision: currentDecision,
                LastAppliedUtc: nowUtc,
                Reason: reason);
        }

        if (!currentDecision.ForceEnforceZOrder
            && currentDecision.Equals(lastDecision.Value)
            && (nowUtc - lastAppliedUtc).TotalMilliseconds < minIntervalMs)
        {
            return new SurfaceZOrderDecisionDedupDecision(
                ShouldApply: false,
                LastDecision: lastDecision.Value,
                LastAppliedUtc: lastAppliedUtc,
                Reason: SurfaceZOrderDecisionDedupReason.SkippedWithinDedupWindow);
        }

        return new SurfaceZOrderDecisionDedupDecision(
            ShouldApply: true,
            LastDecision: currentDecision,
            LastAppliedUtc: nowUtc,
            Reason: SurfaceZOrderDecisionDedupReason.Applied);
    }
}
