namespace ClassroomToolkit.App.Paint;

internal static class CrossPageReplayDispatchFailurePolicy
{
    internal static CrossPageReplayQueueDecision Resolve(CrossPageReplayDispatchTarget target)
    {
        return target switch
        {
            CrossPageReplayDispatchTarget.VisualSync => CrossPageReplayQueueDecisionFactory.VisualSync(),
            CrossPageReplayDispatchTarget.Interaction => CrossPageReplayQueueDecisionFactory.Interaction(),
            _ => CrossPageReplayQueueDecisionFactory.None()
        };
    }
}
