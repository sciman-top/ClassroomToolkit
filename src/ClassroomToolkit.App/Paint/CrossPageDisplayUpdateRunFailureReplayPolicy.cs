namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDisplayUpdateRunFailureReplayPolicy
{
    internal static CrossPageReplayQueueDecision Resolve(string source)
    {
        var context = CrossPageUpdateRequestContextFactory.Create(source);
        return context.Kind switch
        {
            CrossPageUpdateSourceKind.VisualSync => CrossPageReplayQueueDecisionFactory.VisualSync(),
            CrossPageUpdateSourceKind.Interaction => CrossPageReplayQueueDecisionFactory.Interaction(),
            _ => CrossPageReplayQueueDecisionFactory.None()
        };
    }
}
