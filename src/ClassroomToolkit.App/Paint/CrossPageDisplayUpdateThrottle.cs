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

internal static class CrossPageDisplayUpdateThrottleDefaults
{
    internal const int ImmediateDelayMs = 0;
    internal const int MinDelayedDispatchMs = 1;
}

internal static class CrossPageDisplayUpdateMinIntervalThresholds
{
    internal const int PanInkActiveMinMs = 24;
    internal const int PanOnlyMinMs = 20;
    internal const int InkOnlyMinMs = 16;
}

internal static class CrossPageDisplayUpdateMinIntervalPolicy
{
    internal static int ResolveMs(
        bool photoPanning,
        bool crossPageDragging,
        bool inkOperationActive,
        int draggingMinIntervalMs,
        int normalMinIntervalMs)
    {
        if (photoPanning || crossPageDragging)
        {
            if (inkOperationActive)
            {
                return Math.Max(draggingMinIntervalMs, CrossPageDisplayUpdateMinIntervalThresholds.PanInkActiveMinMs);
            }

            return Math.Max(draggingMinIntervalMs, CrossPageDisplayUpdateMinIntervalThresholds.PanOnlyMinMs);
        }

        if (inkOperationActive)
        {
            return Math.Max(draggingMinIntervalMs, CrossPageDisplayUpdateMinIntervalThresholds.InkOnlyMinMs);
        }

        return Math.Max(1, normalMinIntervalMs);
    }
}

internal static class CrossPageDisplayUpdateThrottlePolicy
{
    internal static CrossPageDisplayUpdateDispatchDecision Resolve(
        CrossPageDisplayUpdateDispatchSnapshot snapshot,
        double elapsedMs,
        int draggingMinIntervalMs,
        int normalMinIntervalMs)
    {
        return Resolve(
            updatePending: snapshot.Pending,
            photoPanning: snapshot.Panning,
            crossPageDragging: snapshot.Dragging,
            inkOperationActive: snapshot.InkOperationActive,
            elapsedMs: elapsedMs,
            draggingMinIntervalMs: draggingMinIntervalMs,
            normalMinIntervalMs: normalMinIntervalMs);
    }

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
