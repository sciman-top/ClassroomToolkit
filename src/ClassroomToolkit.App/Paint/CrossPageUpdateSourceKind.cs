namespace ClassroomToolkit.App.Paint;

internal enum CrossPageUpdateSourceKind
{
    Interaction = 0,
    VisualSync = 1,
    BackgroundRefresh = 2
}

internal static class CrossPageUpdateSourceClassifier
{
    internal static CrossPageUpdateSourceKind Classify(string source)
    {
        var parsed = CrossPageUpdateSourceParser.Parse(source);
        var baseSource = parsed.BaseSource;
        if (IsVisualSyncSource(baseSource))
        {
            return CrossPageUpdateSourceKind.VisualSync;
        }

        if (baseSource.StartsWith(CrossPageUpdateSources.NeighborPrefix, StringComparison.Ordinal))
        {
            return CrossPageUpdateSourceKind.BackgroundRefresh;
        }

        return CrossPageUpdateSourceKind.Interaction;
    }

    internal static bool IsVisualSyncSource(string source)
    {
        return source.StartsWith(CrossPageUpdateSources.InkStateChanged, StringComparison.Ordinal)
            || source.StartsWith(CrossPageUpdateSources.InkRedrawCompleted, StringComparison.Ordinal)
            || source.StartsWith(CrossPageUpdateSources.RegionEraseCrossPage, StringComparison.Ordinal)
            || source.StartsWith(CrossPageUpdateSources.UndoSnapshot, StringComparison.Ordinal)
            || source.StartsWith(CrossPageUpdateSources.InkShowPrefix, StringComparison.Ordinal);
    }
}
