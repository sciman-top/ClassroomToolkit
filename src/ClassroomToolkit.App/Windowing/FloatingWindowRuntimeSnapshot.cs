namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingWindowRuntimeSnapshot(
    bool OverlayVisible,
    bool OverlayActive,
    bool PhotoActive,
    bool PresentationFullscreen,
    bool WhiteboardActive,
    bool ImageManagerVisible,
    bool LauncherVisible);
