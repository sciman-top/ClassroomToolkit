namespace ClassroomToolkit.App.Paint;

internal static class CrossPageMutationNeighborSeedPolicy
{
    internal static bool ShouldSeedPreviousPageAfterClear(
        bool clearPreservedNeighborInkFrames,
        bool pageChanged,
        int previousPage,
        int currentPage)
    {
        if (!clearPreservedNeighborInkFrames || !pageChanged)
        {
            return false;
        }

        if (previousPage <= 0 || previousPage == currentPage)
        {
            return false;
        }

        return true;
    }
}
