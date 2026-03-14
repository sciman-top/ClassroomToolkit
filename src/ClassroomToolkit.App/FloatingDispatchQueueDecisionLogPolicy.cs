using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

internal static class FloatingDispatchQueueDecisionLogPolicy
{
    internal static bool ShouldLog(FloatingDispatchQueueReason reason)
    {
        return reason == FloatingDispatchQueueReason.MergedIntoQueuedRequest
               || reason == FloatingDispatchQueueReason.QueueDispatchFailed;
    }
}
