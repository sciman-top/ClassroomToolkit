using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

internal static class ZOrderQueueDispatchFailureRollbackStatePolicy
{
    internal static ZOrderRequestRuntimeState Resolve(
        bool queueDispatchFailed,
        ZOrderRequestRuntimeState previousState,
        ZOrderRequestRuntimeState currentState)
    {
        return queueDispatchFailed ? previousState : currentState;
    }
}
