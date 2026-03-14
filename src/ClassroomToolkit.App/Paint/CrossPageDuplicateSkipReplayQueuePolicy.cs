namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDuplicateSkipReplayQueuePolicy
{
    internal static CrossPageReplayQueueDecision Resolve(
        CrossPageDuplicateWindowDecision duplicateDecision,
        CrossPageUpdateSourceKind kind,
        string source)
    {
        if (!duplicateDecision.ShouldSkip)
        {
            return CrossPageReplayQueueDecisionFactory.None();
        }

        if (duplicateDecision.Reason is not CrossPageDuplicateWindowSkipReason.VisualSync
            and not CrossPageDuplicateWindowSkipReason.Interaction)
        {
            return CrossPageReplayQueueDecisionFactory.None();
        }

        return CrossPageReplayQueuePolicy.Resolve(kind, source);
    }
}
