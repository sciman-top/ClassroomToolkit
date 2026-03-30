namespace ClassroomToolkit.App.Windowing;

internal readonly record struct RollCallVisibilityTransitionContext(
    bool RollCallVisible,
    bool RollCallActive,
    bool OverlayVisible);
