namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageUpdateRequestContext(
    string Source,
    string BaseSource,
    CrossPageUpdateSourceKind Kind);

internal static class CrossPageUpdateRequestContextFactory
{
    internal static CrossPageUpdateRequestContext Create(string? source)
    {
        var normalizedSource = CrossPageUpdateSources.Normalize(source);
        var parsed = CrossPageUpdateSourceParser.Parse(normalizedSource);
        var kind = CrossPageUpdateSourceClassifier.Classify(normalizedSource);
        return new CrossPageUpdateRequestContext(
            normalizedSource,
            parsed.BaseSource,
            kind);
    }
}
