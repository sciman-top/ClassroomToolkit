namespace ClassroomToolkit.App.Paint;

internal readonly record struct OverlayLostMouseCaptureExecutionPlan(
    bool ShouldEndPan,
    bool ShouldClearRightClickPending);

internal static class OverlayLostMouseCaptureExecutionPolicy
{
    internal static OverlayLostMouseCaptureExecutionPlan Resolve(
        bool isMousePhotoPanActive,
        bool rightClickPending)
    {
        return new OverlayLostMouseCaptureExecutionPlan(
            ShouldEndPan: isMousePhotoPanActive,
            ShouldClearRightClickPending: rightClickPending);
    }
}
