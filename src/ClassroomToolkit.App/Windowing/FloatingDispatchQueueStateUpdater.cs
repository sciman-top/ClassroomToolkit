using System;

namespace ClassroomToolkit.App.Windowing;

internal static class FloatingDispatchQueueStateUpdater
{
    internal static void ApplyRequest(
        ref FloatingDispatchQueueState state,
        bool forceEnforceZOrder,
        Func<bool> queueApply,
        Action<FloatingDispatchQueueDecision>? onDecision = null,
        Action<Exception>? onDispatchFailure = null)
    {
        state = FloatingDispatchQueueExecutor.RequestApply(
            state,
            forceEnforceZOrder,
            queueApply,
            onDecision,
            onDispatchFailure);
    }

    internal static void ApplyExecuteQueued(
        ref FloatingDispatchQueueState state,
        Action<bool> apply,
        Action<Exception>? onFailure = null)
    {
        state = FloatingDispatchQueueExecutor.ExecuteQueuedApply(
            state,
            apply,
            onFailure);
    }
}
