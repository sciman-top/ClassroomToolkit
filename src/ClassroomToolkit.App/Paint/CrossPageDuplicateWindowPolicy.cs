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
