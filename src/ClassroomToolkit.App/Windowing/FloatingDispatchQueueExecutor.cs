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
            bool dispatched;
            try
            {
                dispatched = queueApply();
            }
            catch (Exception ex)
            {
                onDispatchFailure?.Invoke(ex);
                dispatched = false;
            }

            if (!dispatched)
            {
                var failedDecision = new FloatingDispatchQueueDecision(
                    state,
                    FloatingDispatchQueueAction.None,
                    FloatingDispatchQueueReason.QueueDispatchFailed);
                onDecision?.Invoke(failedDecision);
                return failedDecision.State;
            }
        }

        onDecision?.Invoke(decision);
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
            apply(state.ForceEnforceZOrder);
        }
        catch (Exception ex)
        {
            if (onFailure != null)
            {
                try
                {
                    onFailure(ex);
                }
                catch
                {
                    // Keep queue state recovery isolated from diagnostics callback failures.
                }
            }
        }
        finally
        {
            state = FloatingDispatchQueuePolicy.OnApplyExecuted(state);
        }

        return state;
    }
}
