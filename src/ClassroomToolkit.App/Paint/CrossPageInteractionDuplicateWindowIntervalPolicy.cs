namespace ClassroomToolkit.App.Paint;

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
