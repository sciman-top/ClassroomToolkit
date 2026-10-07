using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageBackgroundDuplicateWindowIntervalPolicy
{
    internal static int ResolveMs(
        string baseSource,
        int defaultMs = CrossPageDuplicateWindowThresholds.BackgroundRefreshMs)
    {
        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborMissing, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.NeighborMissingDelayed, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborMissingMs);
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborSidecar, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborSidecarMs);
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborRender, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageBackgroundDuplicateWindowIntervalThresholds.NeighborRenderMs);
        }

        return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageDuplicateWindowThresholds.MinWindowMs);
    }
}

internal static class CrossPageBackgroundDuplicateWindowIntervalThresholds
{
    internal const int NeighborMissingMs = 36;
    internal const int NeighborSidecarMs = 32;
    internal const int NeighborRenderMs = 28;
}

internal static class CrossPageBackgroundDuplicateWindowPolicy
{
    internal static bool ShouldSkip(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext? lastRequest,
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int duplicateWindowMs = CrossPageDuplicateWindowThresholds.BackgroundRefreshMs)
    {
        if (!CrossPageDuplicateWindowCorePolicy.TryGetLastRequest(
                lastRequest,
                lastRequestedUtc,
                out var previousRequest))
        {
            return false;
        }

        if (currentRequest.Kind != CrossPageUpdateSourceKind.BackgroundRefresh
            || previousRequest.Kind != CrossPageUpdateSourceKind.BackgroundRefresh)
        {
            return false;
        }

        if (!CrossPageDuplicateWindowCorePolicy.HasSameBaseSource(currentRequest, previousRequest))
        {
            return false;
        }

        var intervalMs = CrossPageBackgroundDuplicateWindowIntervalPolicy.ResolveMs(
            currentRequest.BaseSource,
            duplicateWindowMs);
        return CrossPageDuplicateWindowCorePolicy.IsWithinWindow(nowUtc, lastRequestedUtc, intervalMs);
    }
}

internal static class CrossPageDuplicateWindowCorePolicy
{
    internal static bool TryGetLastRequest(
        CrossPageUpdateRequestContext? lastRequest,
        DateTime lastRequestedUtc,
        out CrossPageUpdateRequestContext value)
    {
        if (lastRequestedUtc != CrossPageRuntimeDefaults.UnsetTimestampUtc
            && lastRequest.HasValue)
        {
            value = lastRequest.Value;
            return true;
        }

        value = default;
        return false;
    }

    internal static bool HasSameBaseSource(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext lastRequest)
    {
        return string.Equals(currentRequest.BaseSource, lastRequest.BaseSource, StringComparison.Ordinal);
    }

    internal static bool IsWithinWindow(
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int intervalMs)
    {
        return (nowUtc - lastRequestedUtc).TotalMilliseconds < intervalMs;
    }
}

internal static class CrossPageDuplicateWindowIntervalPolicy
{
    internal static int Resolve(
        int configuredWindowMs,
        int minimumWindowMs)
    {
        var normalizedConfigured = Math.Max(CrossPageDuplicateWindowThresholds.MinWindowMs, configuredWindowMs);
        return Math.Max(
            normalizedConfigured,
            Math.Max(CrossPageDuplicateWindowThresholds.MinWindowMs, minimumWindowMs));
    }
}

internal enum CrossPageDuplicateWindowSkipReason
{
    None = 0,
    VisualSync = 1,
    BackgroundRefresh = 2,
    Interaction = 3
}

internal readonly record struct CrossPageDuplicateWindowDecision(
    bool ShouldSkip,
    CrossPageDuplicateWindowSkipReason Reason);

internal static class CrossPageDuplicateWindowPolicy
{
    internal static CrossPageDuplicateWindowDecision Resolve(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestRuntimeState state,
        DateTime nowUtc)
    {
        return Resolve(
            currentRequest,
            state.LastRequest,
            nowUtc,
            state.LastRequestUtc);
    }

    internal static CrossPageDuplicateWindowDecision Resolve(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext? lastRequest,
        DateTime nowUtc,
        DateTime lastRequestedUtc)
    {
        if (CrossPageVisualSyncDuplicateWindowPolicy.ShouldSkip(
                currentRequest,
                lastRequest,
                nowUtc,
                lastRequestedUtc))
        {
            return new CrossPageDuplicateWindowDecision(
                ShouldSkip: true,
                Reason: CrossPageDuplicateWindowSkipReason.VisualSync);
        }

        if (CrossPageBackgroundDuplicateWindowPolicy.ShouldSkip(
                currentRequest,
                lastRequest,
                nowUtc,
                lastRequestedUtc))
        {
            return new CrossPageDuplicateWindowDecision(
                ShouldSkip: true,
                Reason: CrossPageDuplicateWindowSkipReason.BackgroundRefresh);
        }

        if (CrossPageInteractionDuplicateWindowPolicy.ShouldSkip(
                currentRequest,
                lastRequest,
                nowUtc,
                lastRequestedUtc))
        {
            return new CrossPageDuplicateWindowDecision(
                ShouldSkip: true,
                Reason: CrossPageDuplicateWindowSkipReason.Interaction);
        }

        return new CrossPageDuplicateWindowDecision(
            ShouldSkip: false,
            Reason: CrossPageDuplicateWindowSkipReason.None);
    }
}

internal static class CrossPageDuplicateWindowThresholds
{
    internal const int MinWindowMs = 1;
    internal const int VisualSyncMs = 12;
    internal const int BackgroundRefreshMs = 24;
    internal const int InteractionMs = 8;
}

internal static class CrossPageInteractionActivityPolicy
{
    internal static bool IsActive(
        bool photoPanning,
        bool crossPageDragging,
        bool inkOperationActive)
    {
        return photoPanning || crossPageDragging || inkOperationActive;
    }
}

internal static class CrossPageInteractionDuplicateWindowIntervalPolicy
{
    internal static int ResolveMs(
        string baseSource,
        int defaultMs = CrossPageDuplicateWindowThresholds.InteractionMs)
    {
        if (baseSource.StartsWith(CrossPageUpdateSources.PhotoPan, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.ManipulationDelta, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.StepViewport, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.ApplyScale, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(
                defaultMs,
                CrossPageInteractionDuplicateWindowIntervalThresholds.PhotoPanLikeMs);
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.PointerUpFast, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(
                defaultMs,
                CrossPageInteractionDuplicateWindowIntervalThresholds.PointerUpFastMs);
        }

        return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageDuplicateWindowThresholds.MinWindowMs);
    }
}

internal static class CrossPageInteractionDuplicateWindowIntervalThresholds
{
    internal const int PhotoPanLikeMs = 24;
    internal const int PointerUpFastMs = 18;
}

internal static class CrossPageInteractionDuplicateWindowPolicy
{
    internal static bool ShouldSkip(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext? lastRequest,
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int duplicateWindowMs = CrossPageDuplicateWindowThresholds.InteractionMs)
    {
        if (!CrossPageDuplicateWindowCorePolicy.TryGetLastRequest(
                lastRequest,
                lastRequestedUtc,
                out var previousRequest))
        {
            return false;
        }

        if (currentRequest.Kind != CrossPageUpdateSourceKind.Interaction
            || previousRequest.Kind != CrossPageUpdateSourceKind.Interaction)
        {
            return false;
        }

        if (!CrossPageDuplicateWindowCorePolicy.HasSameBaseSource(currentRequest, previousRequest))
        {
            return false;
        }

        var intervalMs = CrossPageInteractionDuplicateWindowIntervalPolicy.ResolveMs(
            currentRequest.BaseSource,
            duplicateWindowMs);
        return CrossPageDuplicateWindowCorePolicy.IsWithinWindow(nowUtc, lastRequestedUtc, intervalMs);
    }
}

internal static class CrossPageVisualSyncDuplicateWindowIntervalPolicy
{
    internal static int ResolveMs(
        string baseSource,
        int defaultMs = CrossPageDuplicateWindowThresholds.VisualSyncMs)
    {
        if (baseSource.StartsWith(CrossPageUpdateSources.UndoSnapshot, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageVisualSyncDuplicateWindowIntervalThresholds.UndoMs);
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.RegionEraseCrossPage, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageVisualSyncDuplicateWindowIntervalThresholds.RegionEraseMs);
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.InkRedrawCompleted, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageVisualSyncDuplicateWindowIntervalThresholds.InkRedrawCompletedMs);
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.InkStateChanged, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.InkShowPrefix, StringComparison.Ordinal))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageVisualSyncDuplicateWindowIntervalThresholds.InkStateChangedMs);
        }

        if (CrossPageUpdateReplayPolicy.IsReplayBaseSource(baseSource))
        {
            return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageVisualSyncDuplicateWindowIntervalThresholds.ReplayMs);
        }

        return CrossPageDuplicateWindowIntervalPolicy.Resolve(defaultMs, CrossPageDuplicateWindowThresholds.MinWindowMs);
    }
}

internal static class CrossPageVisualSyncDuplicateWindowIntervalThresholds
{
    internal const int UndoMs = 24;
    internal const int RegionEraseMs = 20;
    internal const int InkRedrawCompletedMs = 18;
    internal const int InkStateChangedMs = 14;
    internal const int ReplayMs = 22;
}

internal static class CrossPageVisualSyncDuplicateWindowPolicy
{
    internal static bool ShouldSkip(
        CrossPageUpdateRequestContext currentRequest,
        CrossPageUpdateRequestContext? lastRequest,
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int duplicateWindowMs = CrossPageDuplicateWindowThresholds.VisualSyncMs)
    {
        if (!CrossPageDuplicateWindowCorePolicy.TryGetLastRequest(
                lastRequest,
                lastRequestedUtc,
                out var previousRequest))
        {
            return false;
        }

        var bothVisualSync = currentRequest.Kind == CrossPageUpdateSourceKind.VisualSync
            && previousRequest.Kind == CrossPageUpdateSourceKind.VisualSync;
        var bothReplaySource = CrossPageUpdateReplayPolicy.IsReplayBaseSource(currentRequest.BaseSource)
            && CrossPageUpdateReplayPolicy.IsReplayBaseSource(previousRequest.BaseSource);
        if (bothReplaySource)
        {
            // Replay is the recovery path for skipped/pending updates.
            // Deduplicating replay requests can leave previous page visuals stale until next interaction.
            return false;
        }

        if (!bothVisualSync)
        {
            return false;
        }

        if (!CrossPageDuplicateWindowCorePolicy.HasSameBaseSource(currentRequest, previousRequest))
        {
            return false;
        }

        var intervalMs = CrossPageVisualSyncDuplicateWindowIntervalPolicy.ResolveMs(
            currentRequest.BaseSource,
            duplicateWindowMs);
        return CrossPageDuplicateWindowCorePolicy.IsWithinWindow(nowUtc, lastRequestedUtc, intervalMs);
    }

    internal static bool ShouldSkip(
        string source,
        string? lastSource,
        DateTime nowUtc,
        DateTime lastRequestedUtc,
        int duplicateWindowMs = CrossPageDuplicateWindowThresholds.VisualSyncMs)
    {
        var currentRequest = CrossPageUpdateRequestContextFactory.Create(source);
        var previousRequest = string.IsNullOrWhiteSpace(lastSource)
            ? (CrossPageUpdateRequestContext?)null
            : CrossPageUpdateRequestContextFactory.Create(lastSource);
        return ShouldSkip(
            currentRequest,
            previousRequest,
            nowUtc,
            lastRequestedUtc,
            duplicateWindowMs);
    }
}
