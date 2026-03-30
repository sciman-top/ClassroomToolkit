namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ForegroundSurfaceActivityState(
    bool OverlayExists,
    bool PhotoModeActive,
    bool WhiteboardActive);
