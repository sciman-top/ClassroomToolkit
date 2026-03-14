namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputSwitchTargetPolicy
{
    internal static int ResolveNeighborTargetPage(
        int currentPage,
        int requestedPage)
    {
        if (requestedPage == currentPage)
        {
            return currentPage;
        }

        if (requestedPage > currentPage)
        {
            return currentPage + 1;
        }

        return currentPage - 1;
    }
}
