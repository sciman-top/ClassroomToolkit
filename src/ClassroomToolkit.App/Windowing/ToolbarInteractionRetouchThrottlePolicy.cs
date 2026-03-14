using System;

namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionRetouchThrottleReason
{
    None = 0,
    FirstRetouch = 1,
    IntervalDisabled = 2,
    WithinThrottleWindow = 3,
    OutsideThrottleWindow = 4
}

internal readonly record struct ToolbarInteractionRetouchThrottleDecision(
    bool ShouldAllow,
    ToolbarInteractionRetouchThrottleReason Reason);

internal static class ToolbarInteractionRetouchThrottlePolicy
{
    internal static ToolbarInteractionRetouchThrottleDecision Resolve(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        var decision = RetouchThrottlePolicy.Resolve(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
        return new ToolbarInteractionRetouchThrottleDecision(
            ShouldAllow: decision.ShouldAllow,
            Reason: decision.Reason switch
            {
                RetouchThrottleReason.FirstRetouch => ToolbarInteractionRetouchThrottleReason.FirstRetouch,
                RetouchThrottleReason.IntervalDisabled => ToolbarInteractionRetouchThrottleReason.IntervalDisabled,
                RetouchThrottleReason.WithinThrottleWindow => ToolbarInteractionRetouchThrottleReason.WithinThrottleWindow,
                RetouchThrottleReason.OutsideThrottleWindow => ToolbarInteractionRetouchThrottleReason.OutsideThrottleWindow,
                _ => ToolbarInteractionRetouchThrottleReason.None
            });
    }

    internal static bool ShouldAllowRetouch(
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
