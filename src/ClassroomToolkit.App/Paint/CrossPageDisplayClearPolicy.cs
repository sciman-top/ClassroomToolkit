namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDisplayClearPolicy
{
    internal static bool ShouldClearNeighborPages(
        int totalPages,
        bool hasCurrentBitmap,
        double currentPageHeight)
    {
        if (totalPages <= 1)
        {
            return true;
        }

        if (!hasCurrentBitmap)
        {
            return true;
        }

        return currentPageHeight <= 0;
    }
}
