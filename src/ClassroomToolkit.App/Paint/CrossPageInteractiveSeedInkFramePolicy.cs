namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveSeedInkFramePolicy
{
    internal static bool ShouldReplaceFrame(
        bool inkShowEnabled,
        bool hasCurrentFrame,
        bool hasResolvedTargetFrame,
        bool slotPageChanged)
    {
        if (!inkShowEnabled)
        {
            return true;
        }

        if (hasResolvedTargetFrame)
        {
            return true;
        }

        if (slotPageChanged)
        {
            // Slot remapped to another page and target ink frame is still unresolved:
            // clear stale old-page frame to avoid one-frame flash on seam crossing.
            return true;
        }

        // Keep current frame when target bitmap is temporarily unavailable.
        return !hasCurrentFrame;
    }
}
