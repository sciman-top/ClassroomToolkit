namespace ClassroomToolkit.App.Paint;

internal readonly record struct PhotoRightButtonDownExecutionPlan(
    bool ShouldArmPending,
    bool ShouldTryBeginPan);

internal static class PhotoRightButtonDownExecutionPolicy
{
    internal static PhotoRightButtonDownExecutionPlan Resolve(
        bool shouldArmPending,
        bool shouldAllowPan)
    {
        return new PhotoRightButtonDownExecutionPlan(
            ShouldArmPending: shouldArmPending,
            ShouldTryBeginPan: shouldAllowPan);
    }
}
