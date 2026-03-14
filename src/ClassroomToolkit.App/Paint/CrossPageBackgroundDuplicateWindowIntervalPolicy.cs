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
