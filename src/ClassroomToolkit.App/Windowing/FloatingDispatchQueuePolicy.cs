namespace ClassroomToolkit.App.Windowing;

internal enum FloatingDispatchQueueAction
{
    None = 0,
    QueueApply = 1
}

internal enum FloatingDispatchQueueReason
{
    None = 0,
    QueuedNewRequest = 1,
    MergedIntoQueuedRequest = 2,
    QueueDispatchFailed = 3
}

internal readonly record struct FloatingDispatchQueueState(
    bool ApplyQueued,
    bool ForceEnforceZOrder)
{
    public static FloatingDispatchQueueState Default => new(
        ApplyQueued: false,
        ForceEnforceZOrder: false);
}

internal readonly record struct FloatingDispatchQueueDecision(
    FloatingDispatchQueueState State,
    FloatingDispatchQueueAction Action,
    FloatingDispatchQueueReason Reason);

internal static class FloatingDispatchQueuePolicy
{
    internal static FloatingDispatchQueueDecision RequestApply(
        FloatingDispatchQueueState state,
        bool forceEnforceZOrder = false)
    {
        if (state.ApplyQueued)
        {
            var merged = state with
            {
                ForceEnforceZOrder = state.ForceEnforceZOrder || forceEnforceZOrder
            };
            return new FloatingDispatchQueueDecision(
                merged,
                FloatingDispatchQueueAction.None,
                FloatingDispatchQueueReason.MergedIntoQueuedRequest);
        }

        var next = state with
        {
            ApplyQueued = true,
            ForceEnforceZOrder = forceEnforceZOrder
        };
        return new FloatingDispatchQueueDecision(
            next,
            FloatingDispatchQueueAction.QueueApply,
            FloatingDispatchQueueReason.QueuedNewRequest);
    }

    internal static FloatingDispatchQueueState OnApplyExecuted(FloatingDispatchQueueState state)
        => state with
        {
            ApplyQueued = false,
            ForceEnforceZOrder = false
        };
}
