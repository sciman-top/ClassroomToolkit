namespace ClassroomToolkit.App.Paint;

internal static class PhotoNavigationInkLoadTranslatePolicy
{
    internal static double ResolveTranslateYBeforeLoad(
        double currentTranslateY,
        double targetTranslateY,
        bool pageChanged,
        bool photoInkModeActive,
        bool crossPageDisplayActive)
    {
        if (pageChanged && photoInkModeActive && crossPageDisplayActive)
        {
            return targetTranslateY;
        }

        return currentTranslateY;
    }
}
