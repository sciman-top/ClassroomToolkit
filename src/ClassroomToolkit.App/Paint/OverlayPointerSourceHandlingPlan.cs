namespace ClassroomToolkit.App.Paint;

internal readonly record struct OverlayPointerSourceHandlingPlan(
    bool ShouldContinue,
    bool ShouldMarkHandled,
    bool ShouldHideEraserPreview);
