using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageMissingNeighborRefreshDecision(
    bool ShouldSchedule,
    DateTime LastScheduledUtc,
    int DelayMs);

internal static class CrossPageMissingNeighborRefreshPolicy
{
    internal static CrossPageMissingNeighborRefreshDecision Resolve(
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive,
        int missingCount,
        DateTime lastScheduledUtc,
        DateTime nowUtc,
        int minIntervalMs = CrossPageMissingNeighborRefreshThresholds.MinIntervalMs,
        int delayMs = CrossPageMissingNeighborRefreshThresholds.DelayMs,
        int interactionMinIntervalMs = CrossPageMissingNeighborRefreshThresholds.InteractionMinIntervalMs,
        int interactionDelayMs = CrossPageMissingNeighborRefreshThresholds.InteractionDelayMs,
        int interactionMissingThreshold = CrossPageMissingNeighborRefreshThresholds.InteractionMissingThreshold)
    {
        var normalizedMinIntervalMs = Math.Max(
            CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs,
            minIntervalMs);
        var normalizedDelayMs = Math.Max(
            CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs,
            delayMs);
        var normalizedInteractionMinIntervalMs = Math.Max(
            normalizedMinIntervalMs,
            Math.Max(
                CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs,
                interactionMinIntervalMs));
        var normalizedInteractionDelayMs = Math.Max(
            normalizedDelayMs,
            Math.Max(
                CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs,
                interactionDelayMs));
        var normalizedInteractionMissingThreshold = Math.Max(
            CrossPageMissingNeighborRefreshNormalizationDefaults.MinMissingThreshold,
            interactionMissingThreshold);

        if (!photoModeActive || !crossPageDisplayEnabled || missingCount <= 0)
        {
            return new CrossPageMissingNeighborRefreshDecision(
                ShouldSchedule: false,
                LastScheduledUtc: lastScheduledUtc,
                DelayMs: normalizedDelayMs);
        }

        var effectiveMinIntervalMs = normalizedMinIntervalMs;
        var effectiveDelayMs = normalizedDelayMs;
        if (interactionActive)
        {
            if (missingCount < normalizedInteractionMissingThreshold)
            {
                return new CrossPageMissingNeighborRefreshDecision(
                    ShouldSchedule: false,
                    LastScheduledUtc: lastScheduledUtc,
                    DelayMs: normalizedDelayMs);
            }

            effectiveMinIntervalMs = normalizedInteractionMinIntervalMs;
            effectiveDelayMs = normalizedInteractionDelayMs;
        }

        if (lastScheduledUtc != CrossPageRuntimeDefaults.UnsetTimestampUtc
            && (nowUtc - lastScheduledUtc).TotalMilliseconds < effectiveMinIntervalMs)
        {
            return new CrossPageMissingNeighborRefreshDecision(
                ShouldSchedule: false,
                LastScheduledUtc: lastScheduledUtc,
                DelayMs: effectiveDelayMs);
        }

        return new CrossPageMissingNeighborRefreshDecision(
            ShouldSchedule: true,
            LastScheduledUtc: nowUtc,
            DelayMs: effectiveDelayMs);
    }
}
