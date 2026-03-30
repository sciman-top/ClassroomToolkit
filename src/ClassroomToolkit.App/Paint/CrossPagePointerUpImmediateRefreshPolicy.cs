namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePointerUpImmediateRefreshPolicy
{
    internal static bool ShouldRequest(
        bool crossPageDisplayActive,
        bool hadInkOperation,
        bool deferredRefreshRequested,
        bool updatePending)
    {
        if (!crossPageDisplayActive)
        {
            return false;
        }

        if (updatePending)
        {
            return false;
        }

        return hadInkOperation || deferredRefreshRequested;
    }
}
