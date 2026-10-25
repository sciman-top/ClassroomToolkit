
namespace ClassroomToolkit.App.Paint;

internal readonly record struct InputInteractionState(
    bool PhotoModeActive,
    bool BoardActive,
    bool CrossPageDisplayEnabled)
{
    internal bool PhotoOrBoardActive => PhotoWindowPolicies.IsPhotoOrBoardActive(PhotoModeActive, BoardActive);
    internal bool PhotoNavigationEnabled => PhotoWindowPolicies.IsPhotoNavigationEnabled(PhotoModeActive, BoardActive);
    internal bool CrossPageDisplayActive => CrossPageInputSwitchPolicies.IsActive(
        PhotoModeActive,
        BoardActive,
        CrossPageDisplayEnabled);
    internal bool CrossPageInputDisplayActive => CrossPageDisplayActive;
}

internal enum PointerDownToolAction
{
    None = 0,
    BeginRegionSelection = 1,
    BeginEraser = 2,
    BeginShape = 3,
    BeginBrushStroke = 4
}

internal readonly record struct PointerDownToolExecutionPlan(
    PointerDownToolAction Action,
    bool ShouldCapturePointer);

internal enum PointerMoveToolAction
{
    None = 0,
    UpdateBrushStroke = 1,
    UpdateEraser = 2,
    UpdateRegionSelection = 3,
    UpdateShapePreview = 4
}

internal enum PointerUpToolAction
{
    None = 0,
    EndBrushStroke = 1,
    EndEraser = 2,
    EndRegionSelection = 3,
    EndShape = 4
}

internal readonly record struct PointerUpToolExecutionPlan(
    PointerUpToolAction Action,
    bool ShouldRefreshAdaptiveRenderer);

internal static class PointerToolExecutionPolicies
{
    internal static InputInteractionState ResolveInputInteractionState(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        return new InputInteractionState(
            PhotoModeActive: photoModeActive,
            BoardActive: boardActive,
            CrossPageDisplayEnabled: crossPageDisplayEnabled);
    }

    internal static bool ShouldDeferCleanup(
        string reason,
        bool mouseCaptured,
        bool stylusCaptured)
    {
        if (!string.Equals(reason, "mouse-capture-lost", StringComparison.Ordinal)
            && !string.Equals(reason, "stylus-capture-lost", StringComparison.Ordinal))
        {
            return false;
        }

        return mouseCaptured || stylusCaptured;
    }

    internal static PointerDownToolExecutionPlan ResolvePointerDownToolExecution(PaintToolMode mode)
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

    internal static PointerMoveToolAction ResolvePointerMoveToolExecution(PaintToolMode mode)
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

    internal static PointerUpToolExecutionPlan ResolvePointerUpToolExecution(
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
