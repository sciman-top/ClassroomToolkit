namespace ClassroomToolkit.App.Paint;

internal static class CrossPageReplayPendingStateUpdater
{
    internal static bool HasPending(CrossPageReplayRuntimeState state)
    {
        return state.VisualSyncReplayPending || state.InteractionReplayPending;
    }

    internal static void ApplyQueueDecision(
        ref CrossPageReplayRuntimeState state,
        CrossPageReplayQueueDecision decision)
    {
        state = state with
        {
            VisualSyncReplayPending = state.VisualSyncReplayPending || decision.QueueVisualSyncReplay,
            InteractionReplayPending = state.InteractionReplayPending || decision.QueueInteractionReplay,
            PreferInteractionReplay = state.PreferInteractionReplay
                || (decision.QueueVisualSyncReplay && decision.QueueInteractionReplay)
        };
    }

    internal static bool TryMarkDispatchScheduled(
        ref CrossPageReplayRuntimeState state,
        CrossPageReplayDispatchTarget target)
    {
        if (target == CrossPageReplayDispatchTarget.VisualSync)
        {
            state = state with
            {
                VisualSyncReplayPending = false,
                LastDispatchTarget = target
            };
            return true;
        }

        if (target == CrossPageReplayDispatchTarget.Interaction)
        {
            state = state with
            {
                InteractionReplayPending = false,
                PreferInteractionReplay = false,
                LastDispatchTarget = target
            };
            return true;
        }

        return false;
    }

    internal static void MarkDispatchFailed(
        ref CrossPageReplayRuntimeState state,
        CrossPageReplayDispatchTarget target)
    {
        var decision = CrossPageReplayDispatchFailurePolicy.Resolve(target);
        ApplyQueueDecision(ref state, decision);
    }

    internal static void Reset(ref CrossPageReplayRuntimeState state)
    {
        state = CrossPageReplayRuntimeState.Default;
    }

    internal static void ApplyQueueDecision(
        ref bool visualSyncReplayPending,
        ref bool interactionReplayPending,
        CrossPageReplayQueueDecision decision)
    {
        visualSyncReplayPending |= decision.QueueVisualSyncReplay;
        interactionReplayPending |= decision.QueueInteractionReplay;
    }

    internal static void MarkDispatchFailed(
        ref bool visualSyncReplayPending,
        ref bool interactionReplayPending,
        CrossPageReplayDispatchTarget target)
    {
        var decision = CrossPageReplayDispatchFailurePolicy.Resolve(target);
        ApplyQueueDecision(
            ref visualSyncReplayPending,
            ref interactionReplayPending,
            decision);
    }

    internal static void Reset(
        ref bool visualSyncReplayPending,
        ref bool interactionReplayPending)
    {
        visualSyncReplayPending = false;
        interactionReplayPending = false;
    }
}
