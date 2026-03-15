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
            // Prefer correctness over transition smoothness while mutating ink across seam.
            return new CrossPageInputSwitchNavigationPlan(
                InteractiveSwitch: false,
                DeferCrossPageDisplayUpdate: false);
        }

        return new CrossPageInputSwitchNavigationPlan(
            InteractiveSwitch: true,
            DeferCrossPageDisplayUpdate: true);
    }
}
