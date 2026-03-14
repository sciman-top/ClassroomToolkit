namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveSeedInkFramePolicy
{
    internal static bool ShouldReplaceFrame(
        bool inkShowEnabled,
        bool hasCurrentFrame,
        bool hasResolvedTargetFrame)
    {
        if (!inkShowEnabled)
        {
            return true;
        }

        if (hasResolvedTargetFrame)
        {
            return true;
        }

        // Keep current frame when target bitmap is temporarily unavailable.
        return !hasCurrentFrame;
    }
}
