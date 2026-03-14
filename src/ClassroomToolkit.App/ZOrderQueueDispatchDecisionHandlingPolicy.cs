using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

internal readonly record struct ZOrderQueueDispatchDecisionHandlingPlan(
    bool ShouldLogDecision,
    bool ShouldMarkQueueDispatchFailed);

internal static class ZOrderQueueDispatchDecisionHandlingPolicy
{
    internal static ZOrderQueueDispatchDecisionHandlingPlan Resolve(FloatingDispatchQueueReason reason)
    {
        return new ZOrderQueueDispatchDecisionHandlingPlan(
            ShouldLogDecision: FloatingDispatchQueueDecisionLogPolicy.ShouldLog(reason),
            ShouldMarkQueueDispatchFailed: ZOrderRequestQueueDispatchFailureRollbackPolicy.ShouldRollback(reason));
    }
}
