namespace ClassroomToolkit.App.Paint;

internal static class PointerMoveToolExecutionPolicy
{
    internal static PointerMoveToolAction Resolve(PaintToolMode mode)
    {
        return mode switch
        {
            PaintToolMode.Brush => PointerMoveToolAction.UpdateBrushStroke,
            PaintToolMode.Eraser => PointerMoveToolAction.UpdateEraser,
            PaintToolMode.RegionErase => PointerMoveToolAction.UpdateRegionSelection,
            PaintToolMode.Shape => PointerMoveToolAction.UpdateShapePreview,
            _ => PointerMoveToolAction.None
        };
    }
}
