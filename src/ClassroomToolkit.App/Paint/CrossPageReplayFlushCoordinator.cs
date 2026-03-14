namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageReplayFlushExecutionResult(
    bool ShouldFlush,
    bool HasDispatchTarget,
    CrossPageReplayDispatchTarget DispatchTarget);

internal static class CrossPageReplayFlushCoordinator
{
    internal static CrossPageReplayFlushExecutionResult Resolve(
        CrossPageReplayRuntimeState replayState,
        bool crossPageUpdatePending,
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        var replayPending = CrossPageReplayPendingStateUpdater.HasPending(replayState);
        var shouldFlush = CrossPageUpdateReplayPolicy.ShouldFlushReplay(
            replayPending,
            crossPageUpdatePending,
            photoModeActive,
            crossPageDisplayEnabled,
            interactionActive);
        if (!shouldFlush)
        {
            return new CrossPageReplayFlushExecutionResult(
                ShouldFlush: false,
                HasDispatchTarget: false,
                DispatchTarget: CrossPageReplayDispatchTarget.None);
        }

        var target = CrossPageReplayDispatchPolicy.Resolve(
            replayState.VisualSyncReplayPending,
            replayState.InteractionReplayPending,
            replayState.LastDispatchTarget,
            replayState.PreferInteractionReplay);
        if (target == CrossPageReplayDispatchTarget.None)
        {
            return new CrossPageReplayFlushExecutionResult(
                ShouldFlush: true,
                HasDispatchTarget: false,
                DispatchTarget: CrossPageReplayDispatchTarget.None);
        }

        return new CrossPageReplayFlushExecutionResult(
            ShouldFlush: true,
            HasDispatchTarget: true,
            DispatchTarget: target);
    }
}
