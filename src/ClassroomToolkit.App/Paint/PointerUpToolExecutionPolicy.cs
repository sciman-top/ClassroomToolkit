namespace ClassroomToolkit.App.Paint;

internal static class PointerUpToolExecutionPolicy
{
    internal static PointerUpToolExecutionPlan Resolve(
        PaintToolMode mode,
        bool pendingAdaptiveRendererRefresh)
    {
        return mode switch
        {
            PaintToolMode.Brush => new PointerUpToolExecutionPlan(
                PointerUpToolAction.EndBrushStroke,
                ShouldRefreshAdaptiveRenderer: pendingAdaptiveRendererRefresh),
            PaintToolMode.Eraser => new PointerUpToolExecutionPlan(
                PointerUpToolAction.EndEraser,
                ShouldRefreshAdaptiveRenderer: false),
            PaintToolMode.RegionErase => new PointerUpToolExecutionPlan(
                PointerUpToolAction.EndRegionSelection,
                ShouldRefreshAdaptiveRenderer: false),
            PaintToolMode.Shape => new PointerUpToolExecutionPlan(
                PointerUpToolAction.EndShape,
                ShouldRefreshAdaptiveRenderer: false),
            _ => new PointerUpToolExecutionPlan(
                PointerUpToolAction.None,
                ShouldRefreshAdaptiveRenderer: false)
        };
    }
}
