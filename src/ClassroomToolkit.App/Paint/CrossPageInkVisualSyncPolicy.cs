namespace ClassroomToolkit.App.Paint;

internal enum CrossPageInkVisualSyncTrigger
{
    InkStateChanged = 0,
    InkRedrawCompleted = 1
}

internal readonly record struct CrossPageInkVisualSyncDecision(
    bool ShouldPrimeVisibleNeighborSlots,
    bool ShouldRequestCrossPageUpdate);

internal static class CrossPageInkVisualSyncPolicy
{
    internal static CrossPageInkVisualSyncDecision Resolve(
        bool photoModeActive,
        bool crossPageDisplayEnabled,
        CrossPageInkVisualSyncTrigger trigger)
    {
        var enabled = photoModeActive && crossPageDisplayEnabled;
        if (!enabled)
        {
            return new CrossPageInkVisualSyncDecision(
                ShouldPrimeVisibleNeighborSlots: false,
                ShouldRequestCrossPageUpdate: false);
        }

        var shouldPrime = trigger == CrossPageInkVisualSyncTrigger.InkStateChanged;
        return new CrossPageInkVisualSyncDecision(
            ShouldPrimeVisibleNeighborSlots: shouldPrime,
            ShouldRequestCrossPageUpdate: true);
    }
}

