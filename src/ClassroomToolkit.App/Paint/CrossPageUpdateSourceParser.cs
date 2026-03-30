namespace ClassroomToolkit.App.Paint;

internal enum CrossPageUpdateDispatchSuffix
{
    None = 0,
    Immediate = 1,
    Delayed = 2
}

internal readonly record struct CrossPageUpdateSourceParseResult(
    string BaseSource,
    CrossPageUpdateDispatchSuffix Suffix);

internal static class CrossPageUpdateSourceParser
{
    internal static CrossPageUpdateSourceParseResult Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return new CrossPageUpdateSourceParseResult(
                CrossPageUpdateSources.Unspecified,
                CrossPageUpdateDispatchSuffix.None);
        }

        if (source.EndsWith(CrossPageUpdateSources.ImmediateSuffix, StringComparison.Ordinal))
        {
            var baseSource = source[..^CrossPageUpdateSources.ImmediateSuffix.Length];
            if (string.IsNullOrWhiteSpace(baseSource))
            {
                baseSource = CrossPageUpdateSources.Unspecified;
            }
            return new CrossPageUpdateSourceParseResult(
                baseSource,
                CrossPageUpdateDispatchSuffix.Immediate);
        }

        if (source.EndsWith(CrossPageUpdateSources.DelayedSuffix, StringComparison.Ordinal))
        {
            var baseSource = source[..^CrossPageUpdateSources.DelayedSuffix.Length];
            if (string.IsNullOrWhiteSpace(baseSource))
            {
                baseSource = CrossPageUpdateSources.Unspecified;
            }
            return new CrossPageUpdateSourceParseResult(
                baseSource,
                CrossPageUpdateDispatchSuffix.Delayed);
        }

        return new CrossPageUpdateSourceParseResult(
            source,
            CrossPageUpdateDispatchSuffix.None);
    }
}
