namespace ClassroomToolkit.App.Paint;

internal static class CrossPageMutationNeighborRetentionPolicy
{
    internal static int ResolvePreservedPage(
        bool clearPreservedNeighborInkFrames,
        bool pageChanged,
        int previousPage,
        int currentPage)
    {
        if (!clearPreservedNeighborInkFrames || !pageChanged)
        {
            return 0;
        }

        if (previousPage <= 0 || previousPage == currentPage)
        {
            return 0;
        }

        return previousPage;
    }
}
