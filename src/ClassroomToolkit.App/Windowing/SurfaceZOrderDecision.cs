namespace ClassroomToolkit.App.Windowing;

internal readonly record struct SurfaceZOrderDecision(
    bool ShouldTouchSurface,
    ZOrderSurface Surface,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);
