namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveInkReplacementPolicy
{
    internal static bool ShouldReplace(
        bool hasResolvedInkBitmap,
        bool holdInkReplacement,
        bool hasCurrentInkFrame,
        bool slotPageChanged)
    {
        if (!hasResolvedInkBitmap)
        {
            return false;
        }

        // Slot changed means target page changed; keeping previous page ink causes
        // visual duplication across pages. Always switch to resolved target frame.
        if (slotPageChanged)
        {
            return true;
        }

        // For same-slot updates, allow replacement unless hold explicitly blocks it.
        return !holdInkReplacement || !hasCurrentInkFrame;
    }
}
