using System;

namespace ClassroomToolkit.App.Paint;

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

internal static class CrossPageDuplicateWindowPolicy
{
    private const int MinimumWindowMs = 1;
    private const int VisualSyncWindowMs = 12;
    private const int BackgroundRefreshWindowMs = 24;
    private const int InteractionWindowMs = 8;

    private const int UndoSnapshotWindowMs = 24;
    private const int RegionEraseWindowMs = 20;
    private const int InkRedrawCompletedWindowMs = 18;
    private const int InkStateChangedWindowMs = 14;

    private const int NeighborMissingWindowMs = 36;
    private const int NeighborSidecarWindowMs = 32;
    private const int NeighborRenderWindowMs = 28;

    private const int PhotoPanLikeWindowMs = 24;
    private const int PointerUpFastWindowMs = 18;

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
        if (lastRequestedUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc
            || !lastRequest.HasValue)
        {
            return None();
        }

        var previousRequest = lastRequest.Value;
        if (currentRequest.Kind != previousRequest.Kind)
        {
            return None();
        }

        if (currentRequest.Kind == CrossPageUpdateSourceKind.VisualSync
            && CrossPageUpdateReplayPolicy.IsReplayBaseSource(currentRequest.BaseSource)
            && CrossPageUpdateReplayPolicy.IsReplayBaseSource(previousRequest.BaseSource))
        {
            // Replay is the recovery path for skipped updates; do not deduplicate it.
            return None();
        }

        if (!string.Equals(currentRequest.BaseSource, previousRequest.BaseSource, StringComparison.Ordinal))
        {
            return None();
        }

        var reason = ToSkipReason(currentRequest.Kind);
        if (reason == CrossPageDuplicateWindowSkipReason.None)
        {
            return None();
        }

        var intervalMs = ResolveWindowMs(currentRequest.Kind, currentRequest.BaseSource);
        if ((nowUtc - lastRequestedUtc).TotalMilliseconds >= intervalMs)
        {
            return None();
        }

        return new CrossPageDuplicateWindowDecision(true, reason);
    }

    private static int ResolveWindowMs(CrossPageUpdateSourceKind kind, string baseSource)
    {
        return kind switch
        {
            CrossPageUpdateSourceKind.VisualSync => ResolveVisualSyncWindowMs(baseSource),
            CrossPageUpdateSourceKind.BackgroundRefresh => ResolveBackgroundRefreshWindowMs(baseSource),
            CrossPageUpdateSourceKind.Interaction => ResolveInteractionWindowMs(baseSource),
            _ => MinimumWindowMs
        };
    }

    private static int ResolveVisualSyncWindowMs(string baseSource)
    {
        var minimumWindowMs = MinimumWindowMs;
        if (baseSource.StartsWith(CrossPageUpdateSources.UndoSnapshot, StringComparison.Ordinal))
        {
            minimumWindowMs = UndoSnapshotWindowMs;
        }
        else if (baseSource.StartsWith(CrossPageUpdateSources.RegionEraseCrossPage, StringComparison.Ordinal))
        {
            minimumWindowMs = RegionEraseWindowMs;
        }
        else if (baseSource.StartsWith(CrossPageUpdateSources.InkRedrawCompleted, StringComparison.Ordinal))
        {
            minimumWindowMs = InkRedrawCompletedWindowMs;
        }
        else if (baseSource.StartsWith(CrossPageUpdateSources.InkStateChanged, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.InkShowPrefix, StringComparison.Ordinal))
        {
            minimumWindowMs = InkStateChangedWindowMs;
        }

        return Math.Max(VisualSyncWindowMs, minimumWindowMs);
    }

    private static int ResolveBackgroundRefreshWindowMs(string baseSource)
    {
        var minimumWindowMs = MinimumWindowMs;
        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborMissing, StringComparison.Ordinal))
        {
            minimumWindowMs = NeighborMissingWindowMs;
        }
        else if (baseSource.StartsWith(CrossPageUpdateSources.NeighborSidecar, StringComparison.Ordinal))
        {
            minimumWindowMs = NeighborSidecarWindowMs;
        }
        else if (baseSource.StartsWith(CrossPageUpdateSources.NeighborRender, StringComparison.Ordinal))
        {
            minimumWindowMs = NeighborRenderWindowMs;
        }

        return Math.Max(BackgroundRefreshWindowMs, minimumWindowMs);
    }

    private static int ResolveInteractionWindowMs(string baseSource)
    {
        var minimumWindowMs = MinimumWindowMs;
        if (baseSource.StartsWith(CrossPageUpdateSources.PhotoPan, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.ManipulationDelta, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.StepViewport, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.ApplyScale, StringComparison.Ordinal))
        {
            minimumWindowMs = PhotoPanLikeWindowMs;
        }
        else if (baseSource.StartsWith(CrossPageUpdateSources.PointerUpFast, StringComparison.Ordinal))
        {
            minimumWindowMs = PointerUpFastWindowMs;
        }

        return Math.Max(InteractionWindowMs, minimumWindowMs);
    }

    private static CrossPageDuplicateWindowSkipReason ToSkipReason(CrossPageUpdateSourceKind kind)
    {
        return kind switch
        {
            CrossPageUpdateSourceKind.VisualSync => CrossPageDuplicateWindowSkipReason.VisualSync,
            CrossPageUpdateSourceKind.BackgroundRefresh => CrossPageDuplicateWindowSkipReason.BackgroundRefresh,
            CrossPageUpdateSourceKind.Interaction => CrossPageDuplicateWindowSkipReason.Interaction,
            _ => CrossPageDuplicateWindowSkipReason.None
        };
    }

    private static CrossPageDuplicateWindowDecision None()
    {
        return new CrossPageDuplicateWindowDecision(false, CrossPageDuplicateWindowSkipReason.None);
    }
}
