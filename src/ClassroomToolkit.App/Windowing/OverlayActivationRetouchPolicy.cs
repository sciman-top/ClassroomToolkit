using System;

namespace ClassroomToolkit.App.Windowing;

internal enum OverlayActivationRetouchReason
{
    None = 0,
    NoApplyRequest = 1,
    Throttled = 2,
    Forced = 3
}

internal readonly record struct OverlayActivationRetouchDecision(
    bool ShouldApply,
    bool ShouldUpdateLastRetouchUtc,
    OverlayActivationRetouchReason Reason);

internal static class OverlayActivationRetouchPolicy
{
    internal static OverlayActivationRetouchDecision Resolve(
        SurfaceZOrderDecision decision,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        if (!decision.RequestZOrderApply)
        {
            return new OverlayActivationRetouchDecision(
                ShouldApply: false,
                ShouldUpdateLastRetouchUtc: false,
                Reason: OverlayActivationRetouchReason.NoApplyRequest);
        }

        if (decision.ForceEnforceZOrder)
        {
            return new OverlayActivationRetouchDecision(
                ShouldApply: true,
                ShouldUpdateLastRetouchUtc: false,
                Reason: OverlayActivationRetouchReason.Forced);
        }

        var shouldApply = ForegroundExplicitRetouchThrottlePolicy.ShouldAllowRetouch(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
        return shouldApply
            ? new OverlayActivationRetouchDecision(
                ShouldApply: true,
                ShouldUpdateLastRetouchUtc: true,
                Reason: OverlayActivationRetouchReason.None)
            : new OverlayActivationRetouchDecision(
                ShouldApply: false,
                ShouldUpdateLastRetouchUtc: false,
                Reason: OverlayActivationRetouchReason.Throttled);
    }

    internal static bool ShouldApply(
        SurfaceZOrderDecision decision,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return Resolve(
            decision,
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs).ShouldApply;
    }

    internal static bool ShouldUpdateLastRetouchUtc(
        SurfaceZOrderDecision decision,
        bool shouldApply)
    {
        return shouldApply && decision.RequestZOrderApply && !decision.ForceEnforceZOrder;
    }

    internal static bool ShouldUpdateLastRetouchUtc(OverlayActivationRetouchDecision decision)
    {
        return decision.ShouldUpdateLastRetouchUtc;
    }
}
