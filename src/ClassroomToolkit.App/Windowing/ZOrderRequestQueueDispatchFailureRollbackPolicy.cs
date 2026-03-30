namespace ClassroomToolkit.App.Windowing;

internal static class ZOrderRequestQueueDispatchFailureRollbackPolicy
{
    internal static bool ShouldRollback(FloatingDispatchQueueReason reason)
    {
        return reason == FloatingDispatchQueueReason.QueueDispatchFailed;
    }
}
