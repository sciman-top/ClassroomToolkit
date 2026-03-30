using System;

namespace ClassroomToolkit.App.Windowing;

internal enum ForegroundExplicitRetouchThrottleReason
{
    None = 0,
    Throttled = 1
}

internal readonly record struct ForegroundExplicitRetouchThrottleDecision(
    bool ShouldAllowRetouch,
    ForegroundExplicitRetouchThrottleReason Reason);

internal static class ForegroundExplicitRetouchThrottlePolicy
{
    internal static ForegroundExplicitRetouchThrottleDecision Resolve(
        ExplicitForegroundRetouchRuntimeState state,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return Resolve(
            state.LastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
    }

    internal static ForegroundExplicitRetouchThrottleDecision Resolve(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        var shouldAllow = RetouchThrottlePolicy.ShouldAllow(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
        return shouldAllow
            ? new ForegroundExplicitRetouchThrottleDecision(
                ShouldAllowRetouch: true,
                Reason: ForegroundExplicitRetouchThrottleReason.None)
            : new ForegroundExplicitRetouchThrottleDecision(
                ShouldAllowRetouch: false,
                Reason: ForegroundExplicitRetouchThrottleReason.Throttled);
    }

    internal static bool ShouldAllowRetouch(
        ExplicitForegroundRetouchRuntimeState state,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return Resolve(
            state.LastRetouchUtc,
            nowUtc,
            minimumIntervalMs).ShouldAllowRetouch;
    }

    internal static bool ShouldAllowRetouch(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return Resolve(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs).ShouldAllowRetouch;
    }
}
