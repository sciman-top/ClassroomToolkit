namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborInkPolicy
{
    internal static bool ShouldKeepExistingInkFrame(
        bool slotPageChanged,
        bool hasExistingInkFrame)
    {
        if (slotPageChanged || !hasExistingInkFrame)
        {
            return false;
        }

        // Keep previous frame for the same page until replacement is ready,
        // regardless of interaction state, to avoid one-frame flash.
        return true;
    }
}
