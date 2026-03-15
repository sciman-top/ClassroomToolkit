namespace ClassroomToolkit.App.Paint;

internal static class CrossPageOutOfPageMoveSuppressionPolicy
{
    internal static bool ShouldSuppress(
        bool crossPageDisplayActive,
        PaintToolMode mode,
        bool strokeInProgress,
        bool switchedPageThisFrame,
        bool recentSwitchGraceActive,
        bool hasCurrentPageRect,
        bool pointerInsideCurrentPageRect)
    {
        if (!crossPageDisplayActive
            || mode != PaintToolMode.Brush
            || !strokeInProgress
            || switchedPageThisFrame
            || recentSwitchGraceActive
            || !hasCurrentPageRect)
        {
            return false;
        }

        return !pointerInsideCurrentPageRect;
    }
}
