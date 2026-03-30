namespace ClassroomToolkit.App.Paint;

internal static class OverlayPointerSourceHandlingPolicy
{
    internal static OverlayPointerSourceHandlingPlan Resolve(
        OverlayPointerSourceGateDecision gateDecision,
        bool hideEraserPreviewWhenBlocked)
    {
        bool shouldHide = hideEraserPreviewWhenBlocked &&
                          gateDecision != OverlayPointerSourceGateDecision.Continue;
        return gateDecision switch
        {
            OverlayPointerSourceGateDecision.Continue => new OverlayPointerSourceHandlingPlan(
                ShouldContinue: true,
                ShouldMarkHandled: false,
                ShouldHideEraserPreview: false),
            OverlayPointerSourceGateDecision.Consume => new OverlayPointerSourceHandlingPlan(
                ShouldContinue: false,
                ShouldMarkHandled: true,
                ShouldHideEraserPreview: shouldHide),
            _ => new OverlayPointerSourceHandlingPlan(
                ShouldContinue: false,
                ShouldMarkHandled: false,
                ShouldHideEraserPreview: shouldHide)
        };
    }
}
