namespace ClassroomToolkit.App.Paint;

internal static class PointerDownToolExecutionPolicy
{
    internal static PointerDownToolExecutionPlan Resolve(PaintToolMode mode)
    {
        return mode switch
        {
            PaintToolMode.RegionErase => new PointerDownToolExecutionPlan(
                PointerDownToolAction.BeginRegionSelection,
                ShouldCapturePointer: true),
            PaintToolMode.Eraser => new PointerDownToolExecutionPlan(
                PointerDownToolAction.BeginEraser,
                ShouldCapturePointer: true),
            PaintToolMode.Shape => new PointerDownToolExecutionPlan(
                PointerDownToolAction.BeginShape,
                ShouldCapturePointer: true),
            PaintToolMode.Brush => new PointerDownToolExecutionPlan(
                PointerDownToolAction.BeginBrushStroke,
                ShouldCapturePointer: true),
            _ => new PointerDownToolExecutionPlan(
                PointerDownToolAction.None,
                ShouldCapturePointer: false)
        };
    }
}
