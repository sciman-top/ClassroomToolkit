namespace ClassroomToolkit.App.Paint;

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
