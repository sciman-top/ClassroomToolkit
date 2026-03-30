namespace ClassroomToolkit.App.Paint;

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
