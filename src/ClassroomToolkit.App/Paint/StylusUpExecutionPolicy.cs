namespace ClassroomToolkit.App.Paint;

internal static class StylusUpExecutionPolicy
{
    internal static StylusUpExecutionPlan Resolve(
        bool photoLoading,
        bool handledByPhotoPan,
        bool inkOperationActive,
        bool hasStylusPoints)
    {
        if (photoLoading || handledByPhotoPan || !inkOperationActive)
        {
            return new StylusUpExecutionPlan(
                StylusUpExecutionAction.None,
                ShouldMarkHandled: false);
        }

        return new StylusUpExecutionPlan(
            hasStylusPoints
                ? StylusUpExecutionAction.HandleLastStylusPoint
                : StylusUpExecutionAction.HandlePointerPosition,
            ShouldMarkHandled: true);
    }
}
