namespace ClassroomToolkit.App.Paint;

internal readonly record struct OverlayLostMouseCaptureExecutionPlan(
    bool ShouldEndPan,
    bool ShouldClearRightClickPending,
    bool ShouldCancelInkOperation);

internal static class OverlayLostMouseCaptureExecutionPolicy
{
    internal static OverlayLostMouseCaptureExecutionPlan Resolve(
        bool photoPanning,
        bool rightClickPending,
        bool inkOperationActive)
    {
        return new OverlayLostMouseCaptureExecutionPlan(
            ShouldEndPan: photoPanning,
            ShouldClearRightClickPending: rightClickPending,
            ShouldCancelInkOperation: inkOperationActive);
    }
}
