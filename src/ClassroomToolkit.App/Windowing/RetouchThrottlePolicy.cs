using System;

namespace ClassroomToolkit.App.Windowing;

internal enum RetouchThrottleReason
{
    None = 0,
    IntervalDisabled = 1,
    FirstRetouch = 2,
    WithinThrottleWindow = 3,
    OutsideThrottleWindow = 4
}

internal readonly record struct RetouchThrottleDecision(
    bool ShouldAllow,
    RetouchThrottleReason Reason);

internal static class RetouchThrottlePolicy
{
    internal static RetouchThrottleDecision Resolve(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        if (minimumIntervalMs <= 0)
        {
            return new RetouchThrottleDecision(
                ShouldAllow: true,
                Reason: RetouchThrottleReason.IntervalDisabled);
        }
        if (lastRetouchUtc == WindowDedupDefaults.UnsetTimestampUtc)
        {
            return new RetouchThrottleDecision(
                ShouldAllow: true,
                Reason: RetouchThrottleReason.FirstRetouch);
        }

        var allow = (nowUtc - lastRetouchUtc).TotalMilliseconds >= minimumIntervalMs;
        return allow
            ? new RetouchThrottleDecision(
                ShouldAllow: true,
                Reason: RetouchThrottleReason.OutsideThrottleWindow)
            : new RetouchThrottleDecision(
                ShouldAllow: false,
                Reason: RetouchThrottleReason.WithinThrottleWindow);
    }

    internal static bool ShouldAllow(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return Resolve(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs).ShouldAllow;
    }
}
