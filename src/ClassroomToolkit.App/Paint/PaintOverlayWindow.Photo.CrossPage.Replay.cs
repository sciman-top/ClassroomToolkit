namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void TryFlushCrossPageReplay()
    {
        var flushPlan = CrossPageReplayFlushCoordinator.Resolve(
            replayState: _crossPageReplayState,
            crossPageUpdatePending: _crossPageDisplayUpdateState.Pending,
            photoModeActive: _photoModeActive,
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled(),
            interactionActive: IsCrossPageInteractionActive());
        if (!flushPlan.ShouldFlush || !flushPlan.HasDispatchTarget)
        {
            return;
        }

        TryScheduleCrossPageReplayDispatch(flushPlan.DispatchTarget);
    }

    private bool HasCrossPageReplayPending()
    {
        return CrossPageReplayPendingStateUpdater.HasPending(_crossPageReplayState);
    }

    private void TryFlushCrossPageReplayAfterPointerUp()
    {
        // Pointer-up is the earliest stable point where interaction can end.
        // Triggering replay flush here avoids "old page refreshes only after next pan".
        TryFlushCrossPageReplay();
    }

    private void TryScheduleCrossPageReplayDispatch(CrossPageReplayDispatchTarget target)
    {
        _ = CrossPageReplayDispatchCoordinator.Apply(
            ref _crossPageReplayState,
            target,
            requestCrossPageDisplayUpdate: RequestCrossPageDisplayUpdate,
            tryBeginInvoke: TryBeginInvoke,
            dispatcherCheckAccess: Dispatcher.CheckAccess,
            dispatcherShutdownStarted: () => Dispatcher.HasShutdownStarted,
            dispatcherShutdownFinished: () => Dispatcher.HasShutdownFinished);
    }

    private void ResetCrossPageReplayState()
    {
        CrossPageReplayPendingStateUpdater.Reset(
            ref _crossPageReplayState);
    }
}