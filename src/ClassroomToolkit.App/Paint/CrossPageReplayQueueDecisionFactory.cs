namespace ClassroomToolkit.App.Paint;

internal static class CrossPageReplayQueueDecisionFactory
{
    internal static CrossPageReplayQueueDecision None()
    {
        return new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: false,
            QueueInteractionReplay: false);
    }

    internal static CrossPageReplayQueueDecision VisualSync()
    {
        return new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: true,
            QueueInteractionReplay: false);
    }

    internal static CrossPageReplayQueueDecision Interaction()
    {
        return new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: false,
            QueueInteractionReplay: true);
    }

    internal static CrossPageReplayQueueDecision VisualSyncAndInteraction()
    {
        return new CrossPageReplayQueueDecision(
            QueueVisualSyncReplay: true,
            QueueInteractionReplay: true);
    }
}
