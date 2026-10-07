
namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInkVisualSyncDedupDefaults
{
    internal const int DuplicateWindowMs = 64;
}

internal static class CrossPageInkVisualSyncDedupPolicy
{
    internal static bool ShouldSkip(
        CrossPageInkVisualSyncTrigger trigger,
        CrossPageInkVisualSyncTrigger? lastTrigger,
        bool interactionActive,
        double elapsedSinceLastMs,
        int duplicateWindowMs = CrossPageInkVisualSyncDedupDefaults.DuplicateWindowMs)
    {
        if (interactionActive)
        {
            return false;
        }

        if (trigger != CrossPageInkVisualSyncTrigger.InkRedrawCompleted)
        {
            return false;
        }

        if (lastTrigger != CrossPageInkVisualSyncTrigger.InkStateChanged)
        {
            return false;
        }

        if (duplicateWindowMs <= 0)
        {
            return false;
        }

        return elapsedSinceLastMs >= 0 && elapsedSinceLastMs < duplicateWindowMs;
    }
}

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

internal readonly record struct CrossPageInkVisualSyncRuntimeState(
    DateTime LastSyncUtc,
    CrossPageInkVisualSyncTrigger? LastTrigger)
{
    internal static CrossPageInkVisualSyncRuntimeState Default => new(
        LastSyncUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
        LastTrigger: null);
}

internal static class CrossPageInkVisualSyncStateUpdater
{
    internal static void MarkApplied(
        ref CrossPageInkVisualSyncRuntimeState state,
        DateTime nowUtc,
        CrossPageInkVisualSyncTrigger trigger)
    {
        state = new CrossPageInkVisualSyncRuntimeState(
            LastSyncUtc: nowUtc,
            LastTrigger: trigger);
    }
}
