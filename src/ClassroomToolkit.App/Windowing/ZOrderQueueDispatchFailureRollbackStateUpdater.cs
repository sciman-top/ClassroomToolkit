namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderQueueDispatchFailureRollbackStateUpdater
{
    internal static void Apply(
        ref ZOrderRequestRuntimeState state,
        bool queueDispatchFailed,
        ZOrderRequestRuntimeState previousState)
    {
        state = ZOrderQueueDispatchFailureRollbackStatePolicy.Resolve(
            queueDispatchFailed,
            previousState,
            state);
    }
}
