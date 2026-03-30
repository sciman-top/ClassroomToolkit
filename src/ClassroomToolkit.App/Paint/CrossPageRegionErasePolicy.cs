namespace ClassroomToolkit.App.Paint;

internal static class CrossPageRegionErasePolicy
{
    internal static bool ShouldUseCrossPageErase(
        bool photoInkModeActive,
        bool crossPageDisplayEnabled)
    {
        return photoInkModeActive && crossPageDisplayEnabled;
    }

    internal static bool CanNavigateForRegionErase(
        bool photoInkModeActive,
        bool crossPageDisplayEnabled,
        int targetPage)
    {
        return ShouldUseCrossPageErase(photoInkModeActive, crossPageDisplayEnabled)
               && targetPage > 0;
    }
}
