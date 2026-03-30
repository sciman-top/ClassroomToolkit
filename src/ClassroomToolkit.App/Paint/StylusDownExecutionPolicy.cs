namespace ClassroomToolkit.App.Paint;

internal static class StylusDownExecutionPolicy
{
    internal static StylusDownExecutionPlan Resolve(
        bool photoLoading,
        bool handledByPhotoPan,
        bool shouldIgnoreFromPhotoControls,
        bool hasStylusPoints)
    {
        if (photoLoading || handledByPhotoPan || shouldIgnoreFromPhotoControls)
        {
            return new StylusDownExecutionPlan(
                StylusDownExecutionAction.None,
                ShouldResetTimestampState: false,
                ShouldMarkHandled: false);
        }

        return new StylusDownExecutionPlan(
            hasStylusPoints
                ? StylusDownExecutionAction.HandleFirstStylusPoint
                : StylusDownExecutionAction.HandlePointerPosition,
            ShouldResetTimestampState: true,
            ShouldMarkHandled: true);
    }
}
