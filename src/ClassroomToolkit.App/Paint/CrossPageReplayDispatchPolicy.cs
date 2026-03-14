namespace ClassroomToolkit.App.Paint;

internal enum CrossPageReplayDispatchTarget
{
    None = 0,
    VisualSync = 1,
    Interaction = 2
}

internal static class CrossPageReplayDispatchPolicy
{
    internal static CrossPageReplayDispatchTarget Resolve(
        bool visualSyncReplayPending,
        bool interactionReplayPending)
    {
        if (visualSyncReplayPending)
        {
            return CrossPageReplayDispatchTarget.VisualSync;
        }

        if (interactionReplayPending)
        {
            return CrossPageReplayDispatchTarget.Interaction;
        }

        return CrossPageReplayDispatchTarget.None;
    }

    internal static CrossPageReplayDispatchTarget Resolve(
        bool visualSyncReplayPending,
        bool interactionReplayPending,
        CrossPageReplayDispatchTarget lastDispatchedTarget,
        bool preferInteractionReplay)
    {
        if (!visualSyncReplayPending && !interactionReplayPending)
        {
            return CrossPageReplayDispatchTarget.None;
        }

        if (visualSyncReplayPending && interactionReplayPending)
        {
            if (preferInteractionReplay)
            {
                return CrossPageReplayDispatchTarget.Interaction;
            }

            return lastDispatchedTarget == CrossPageReplayDispatchTarget.VisualSync
                ? CrossPageReplayDispatchTarget.Interaction
                : CrossPageReplayDispatchTarget.VisualSync;
        }

        return Resolve(visualSyncReplayPending, interactionReplayPending);
    }
}
