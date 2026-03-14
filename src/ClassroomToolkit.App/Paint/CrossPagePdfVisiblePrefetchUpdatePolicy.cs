namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePdfVisiblePrefetchUpdatePolicy
{
    internal static bool ShouldRefreshCrossPageDisplay(
        bool photoModeActive,
        bool photoDocumentIsPdf,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        if (!photoDocumentIsPdf)
        {
            return false;
        }

        return PhotoInteractionModePolicy.IsCrossPageDisplayActive(
            photoModeActive,
            boardActive,
            crossPageDisplayEnabled);
    }
}
