namespace ClassroomToolkit.App.Paint;

internal static class StylusMoveExecutionPolicy
{
    internal static StylusMoveExecutionPlan Resolve(
        bool photoLoading,
        bool handledByPhotoPan,
        bool inkOperationActive,
        bool hasStylusPoints,
        PaintToolMode mode,
        bool strokeInProgress)
    {
        if (photoLoading || handledByPhotoPan || !inkOperationActive)
        {
            return new StylusMoveExecutionPlan(
                StylusMoveExecutionAction.None,
                ShouldMarkHandled: false);
        }

        if (!hasStylusPoints)
        {
            return new StylusMoveExecutionPlan(
                StylusMoveExecutionAction.HandlePointerPosition,
                ShouldMarkHandled: true);
        }

        if (mode == PaintToolMode.Brush && strokeInProgress)
        {
            return new StylusMoveExecutionPlan(
                StylusMoveExecutionAction.HandleBrushBatch,
                ShouldMarkHandled: true);
        }

        return new StylusMoveExecutionPlan(
            StylusMoveExecutionAction.HandleStylusPointsIndividually,
            ShouldMarkHandled: true);
    }
}
