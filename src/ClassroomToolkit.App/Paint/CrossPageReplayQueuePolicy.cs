namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageReplayQueueDecision(
    bool QueueVisualSyncReplay,
    bool QueueInteractionReplay);

internal static class CrossPageReplayQueuePolicy
{
    internal static CrossPageReplayQueueDecision Resolve(CrossPageUpdateSourceKind kind)
    {
        return Resolve(kind, source: CrossPageUpdateSources.Unspecified);
    }

    internal static CrossPageReplayQueueDecision Resolve(CrossPageUpdateSourceKind kind, string source)
    {
        if (!CrossPageUpdateReplayPolicy.ShouldQueueReplay(kind))
        {
            return CrossPageReplayQueueDecisionFactory.None();
        }

        var parsed = CrossPageUpdateSourceParser.Parse(source);
        var immediateSuffix = parsed.Suffix == CrossPageUpdateDispatchSuffix.Immediate;

        return kind switch
        {
            CrossPageUpdateSourceKind.VisualSync when immediateSuffix
                => CrossPageReplayQueueDecisionFactory.VisualSyncAndInteraction(),
            CrossPageUpdateSourceKind.VisualSync
                => CrossPageReplayQueueDecisionFactory.VisualSync(),
            CrossPageUpdateSourceKind.Interaction
                => CrossPageReplayQueueDecisionFactory.Interaction(),
            _ => CrossPageReplayQueueDecisionFactory.None()
        };
    }
}
