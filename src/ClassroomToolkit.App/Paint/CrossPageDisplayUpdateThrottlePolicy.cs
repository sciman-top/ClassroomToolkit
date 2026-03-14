namespace ClassroomToolkit.App.Paint;

internal enum CrossPageDisplayUpdateDispatchMode
{
    SkipPending = 0,
    Delayed = 1,
    Direct = 2
}

internal readonly record struct CrossPageDisplayUpdateDispatchDecision(
    CrossPageDisplayUpdateDispatchMode Mode,
    int DelayMs);

internal static class CrossPageDisplayUpdateThrottlePolicy
{
    internal static CrossPageDisplayUpdateDispatchDecision Resolve(
        bool updatePending,
        bool photoPanning,
        bool crossPageDragging,
        bool inkOperationActive,
        double elapsedMs,
        int draggingMinIntervalMs,
        int normalMinIntervalMs)
    {
        if (updatePending)
        {
            return new CrossPageDisplayUpdateDispatchDecision(
                CrossPageDisplayUpdateDispatchMode.SkipPending,
                DelayMs: CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs);
        }

        var throttleActive = CrossPageInteractionActivityPolicy.IsActive(
            photoPanning,
            crossPageDragging,
            inkOperationActive);
        var minIntervalMs = throttleActive
            ? CrossPageDisplayUpdateMinIntervalPolicy.ResolveMs(
                photoPanning,
                crossPageDragging,
                inkOperationActive,
                draggingMinIntervalMs,
                normalMinIntervalMs)
            : normalMinIntervalMs;
        if (throttleActive && elapsedMs < minIntervalMs)
        {
            var delay = Math.Max(
                CrossPageDisplayUpdateThrottleDefaults.MinDelayedDispatchMs,
                (int)Math.Ceiling(minIntervalMs - elapsedMs));
            return new CrossPageDisplayUpdateDispatchDecision(
                CrossPageDisplayUpdateDispatchMode.Delayed,
                delay);
        }

        return new CrossPageDisplayUpdateDispatchDecision(
            CrossPageDisplayUpdateDispatchMode.Direct,
            DelayMs: CrossPageDisplayUpdateThrottleDefaults.ImmediateDelayMs);
    }
}
