namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePostInputDelayPolicy
{
    internal static int ResolveMs(
        string source,
        int configuredDelayMs,
        int fallbackDelayMs = CrossPagePostInputDelayThresholds.FallbackDelayMs,
        int? delayOverrideMs = null)
    {
        var parsed = CrossPageUpdateSourceParser.Parse(source);
        var baseSource = parsed.BaseSource;

        var baseline = delayOverrideMs ?? configuredDelayMs;
        if (baseline <= 0)
        {
            baseline = fallbackDelayMs;
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborRender, StringComparison.Ordinal)
            || baseSource.StartsWith(CrossPageUpdateSources.NeighborSidecar, StringComparison.Ordinal))
        {
            return Math.Max(baseline, CrossPagePostInputDelayThresholds.NeighborRenderMinMs);
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborMissing, StringComparison.Ordinal))
        {
            return Math.Max(baseline, CrossPagePostInputDelayThresholds.NeighborMissingMinMs);
        }

        if (CrossPageUpdateReplayPolicy.IsReplayBaseSource(baseSource))
        {
            return Math.Max(baseline, CrossPagePostInputDelayThresholds.ReplayMinMs);
        }

        return Math.Max(1, baseline);
    }
}
