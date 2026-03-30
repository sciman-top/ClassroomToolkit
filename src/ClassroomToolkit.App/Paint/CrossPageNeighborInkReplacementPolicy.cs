namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborInkReplacementPolicy
{
    internal static bool ShouldReplace(
        bool slotPageChanged,
        bool hasCurrentInkFrame,
        bool usedPreservedInkFrame)
    {
        if (!hasCurrentInkFrame)
        {
            return true;
        }

        if (!slotPageChanged)
        {
            return false;
        }

        // During slot remap, keep preserved frame until a truly newer render arrives.
        return !usedPreservedInkFrame;
    }
}
