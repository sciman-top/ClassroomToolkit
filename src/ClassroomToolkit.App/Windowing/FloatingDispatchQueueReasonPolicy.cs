namespace ClassroomToolkit.App.Windowing;

internal static class FloatingDispatchQueueReasonPolicy
{
    internal static string ResolveTag(FloatingDispatchQueueReason reason)
    {
        return reason switch
        {
            FloatingDispatchQueueReason.QueuedNewRequest => "queued-new-request",
            FloatingDispatchQueueReason.MergedIntoQueuedRequest => "merged-into-queued-request",
            FloatingDispatchQueueReason.QueueDispatchFailed => "queue-dispatch-failed",
            _ => "none"
        };
    }
}
