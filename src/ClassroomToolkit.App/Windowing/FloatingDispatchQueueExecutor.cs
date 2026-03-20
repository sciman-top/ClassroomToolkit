using System;

namespace ClassroomToolkit.App.Windowing;

internal static class FloatingDispatchQueueExecutor
{
    internal static FloatingDispatchQueueState RequestApply(
        FloatingDispatchQueueState state,
        bool forceEnforceZOrder,
        Func<bool> queueApply,
        Action<FloatingDispatchQueueDecision>? onDecision = null,
        Action<Exception>? onDispatchFailure = null)
    {
        ArgumentNullException.ThrowIfNull(queueApply);

        var decision = FloatingDispatchQueuePolicy.RequestApply(state, forceEnforceZOrder);
        if (decision.Action == FloatingDispatchQueueAction.QueueApply)
        {
            Exception? dispatchFailure = null;
            var dispatched = SafeActionExecutionExecutor.TryExecute(
                queueApply,
                fallback: false,
                onFailure: ex => dispatchFailure = ex);
            if (dispatchFailure != null)
            {
                _ = SafeActionExecutionExecutor.TryExecute(
                    () => onDispatchFailure?.Invoke(dispatchFailure));
            }

            if (!dispatched)
            {
                var failedDecision = new FloatingDispatchQueueDecision(
                    state,
                    FloatingDispatchQueueAction.None,
                    FloatingDispatchQueueReason.QueueDispatchFailed);
                _ = SafeActionExecutionExecutor.TryExecute(
                    () => onDecision?.Invoke(failedDecision));
                return failedDecision.State;
            }
        }

        _ = SafeActionExecutionExecutor.TryExecute(
            () => onDecision?.Invoke(decision));
        return decision.State;
    }

    internal static FloatingDispatchQueueState ExecuteQueuedApply(
        FloatingDispatchQueueState state,
        Action<bool> apply,
        Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(apply);

        try
        {
            _ = SafeActionExecutionExecutor.TryExecute(
                () => apply(state.ForceEnforceZOrder),
                onFailure: onFailure);
        }
        finally
        {
            state = FloatingDispatchQueuePolicy.OnApplyExecuted(state);
        }

        return state;
    }
}
