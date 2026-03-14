using System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct OverlayActivationRetouchExecutionResult(
    bool Applied,
    bool SuppressionConsumed,
    OverlayActivationRetouchReason Reason);

internal readonly record struct ExplicitForegroundRetouchExecutionResult(
    bool Applied,
    ForegroundExplicitRetouchThrottleReason Reason);

internal static class ForegroundSurfaceRetouchCoordinator
{
    internal static OverlayActivationRetouchExecutionResult ApplyOverlayActivated(
        bool suppressionConsumed,
        SurfaceZOrderDecision decision,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs,
        Action<DateTime> markRetouched,
        Action<SurfaceZOrderDecision> applySurfaceDecision)
    {
        ArgumentNullException.ThrowIfNull(markRetouched);
        ArgumentNullException.ThrowIfNull(applySurfaceDecision);

        if (suppressionConsumed)
        {
            return new OverlayActivationRetouchExecutionResult(
                Applied: false,
                SuppressionConsumed: true,
                Reason: OverlayActivationRetouchReason.NoApplyRequest);
        }

        var retouchDecision = OverlayActivationRetouchPolicy.Resolve(
            decision,
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
        if (!retouchDecision.ShouldApply)
        {
            return new OverlayActivationRetouchExecutionResult(
                Applied: false,
                SuppressionConsumed: false,
                Reason: retouchDecision.Reason);
        }

        if (OverlayActivationRetouchPolicy.ShouldUpdateLastRetouchUtc(retouchDecision))
        {
            markRetouched(nowUtc);
        }

        applySurfaceDecision(decision);
        return new OverlayActivationRetouchExecutionResult(
            Applied: true,
            SuppressionConsumed: false,
            Reason: retouchDecision.Reason);
    }

    internal static ExplicitForegroundRetouchExecutionResult ApplyExplicitForeground(
        ExplicitForegroundRetouchRuntimeState state,
        DateTime nowUtc,
        int minimumIntervalMs,
        SurfaceZOrderDecision decision,
        Action<DateTime> markRetouched,
        Action<SurfaceZOrderDecision> applySurfaceDecision)
    {
        ArgumentNullException.ThrowIfNull(markRetouched);
        ArgumentNullException.ThrowIfNull(applySurfaceDecision);

        var throttleDecision = ForegroundExplicitRetouchThrottlePolicy.Resolve(
            state,
            nowUtc,
            minimumIntervalMs);
        if (!throttleDecision.ShouldAllowRetouch)
        {
            return new ExplicitForegroundRetouchExecutionResult(
                Applied: false,
                Reason: throttleDecision.Reason);
        }

        markRetouched(nowUtc);
        applySurfaceDecision(decision);
        return new ExplicitForegroundRetouchExecutionResult(
            Applied: true,
            Reason: throttleDecision.Reason);
    }
}
