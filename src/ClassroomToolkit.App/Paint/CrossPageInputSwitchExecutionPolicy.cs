namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputSwitchExecutionPolicy
{
    internal static CrossPageInputSwitchExecutionPlan Resolve(
        int currentPage,
        int targetPage,
        PaintToolMode mode,
        double currentPageHeight)
    {
        if (targetPage == currentPage || targetPage <= 0)
        {
            return new CrossPageInputSwitchExecutionPlan(
                ShouldSwitch: false,
                ShouldResolveBrushContinuation: false,
                DeferCrossPageDisplayUpdate: false);
        }

        var shouldResolveBrushContinuation = mode == PaintToolMode.Brush && currentPageHeight > 0;
        var deferCrossPageDisplayUpdate = mode != PaintToolMode.Brush;
        return new CrossPageInputSwitchExecutionPlan(
            ShouldSwitch: true,
            ShouldResolveBrushContinuation: shouldResolveBrushContinuation,
            DeferCrossPageDisplayUpdate: deferCrossPageDisplayUpdate);
    }
}
