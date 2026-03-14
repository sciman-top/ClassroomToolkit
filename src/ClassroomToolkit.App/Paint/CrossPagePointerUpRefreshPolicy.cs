namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePointerUpRefreshPolicy
{
    internal static bool ShouldSchedulePostInputRefresh(
        bool crossPageDisplayActive,
        bool hadInkOperation,
        bool deferredRefreshRequested)
    {
        if (!crossPageDisplayActive)
        {
            return false;
        }

        return hadInkOperation || deferredRefreshRequested;
    }
}
