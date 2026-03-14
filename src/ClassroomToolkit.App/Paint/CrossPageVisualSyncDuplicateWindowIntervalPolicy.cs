namespace ClassroomToolkit.App.Paint;

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
