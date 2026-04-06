namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageInputSwitchNavigationPlan(
    bool InteractiveSwitch,
    bool DeferCrossPageDisplayUpdate);

internal static class CrossPageInputSwitchNavigationPolicy
{
    internal static CrossPageInputSwitchNavigationPlan Resolve(
        PaintToolMode mode,
        bool strokeInProgress,
        bool isErasing,
        bool isRegionSelecting,
        bool inputTriggeredByActiveInkMutation = false)
    {
        var hasActiveInkMutation = inputTriggeredByActiveInkMutation
            || strokeInProgress
            || isErasing
            || isRegionSelecting;
        if (hasActiveInkMutation
            && (mode == PaintToolMode.Brush
                || mode == PaintToolMode.Eraser
                || mode == PaintToolMode.RegionErase))
        {
            // Brush seam continuation needs the interactive path so the current page and
            // its neighbor frames stay coherent while the stroke crosses the seam.
            if (mode == PaintToolMode.Brush)
            {
                return new CrossPageInputSwitchNavigationPlan(
                    InteractiveSwitch: true,
                    DeferCrossPageDisplayUpdate: true);
            }

            // Eraser and region erase still prefer the stable path because their geometry
            // mutations are not resumed as a continuous stroke across the seam.
            return new CrossPageInputSwitchNavigationPlan(
                InteractiveSwitch: false,
                DeferCrossPageDisplayUpdate: false);
        }

        return new CrossPageInputSwitchNavigationPlan(
            InteractiveSwitch: true,
            DeferCrossPageDisplayUpdate: true);
    }
}
