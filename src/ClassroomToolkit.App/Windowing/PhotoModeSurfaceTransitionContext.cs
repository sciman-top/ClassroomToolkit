namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PhotoModeSurfaceTransitionContext(
    bool PhotoModeActive,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder,
    bool OverlayVisible);
