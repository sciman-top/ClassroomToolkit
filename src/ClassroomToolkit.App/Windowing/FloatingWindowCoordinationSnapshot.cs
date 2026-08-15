namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingWindowCoordinationSnapshot(
    FloatingWindowRuntimeSnapshot Runtime,
    LauncherWindowRuntimeSnapshot Launcher,
    FloatingTopmostVisibilitySnapshot TopmostVisibility,
    FloatingUtilityActivitySnapshot UtilityActivity,
    FloatingOwnerRuntimeSnapshot Owner);

internal readonly record struct FloatingWindowRuntimeSnapshot(
    bool OverlayVisible,
    bool OverlayActive,
    bool PhotoActive,
    bool PresentationFullscreen,
    bool WhiteboardActive,
    bool ImageManagerVisible,
    bool LauncherVisible);

internal readonly record struct FloatingTopmostVisibilitySnapshot(
    bool ToolbarVisible,
    bool RollCallVisible,
    bool LauncherVisible,
    bool ImageManagerVisible,
    bool OverlayVisible);

internal readonly record struct FloatingUtilityActivitySnapshot(
    bool ToolbarActive,
    bool RollCallActive,
    bool ImageManagerActive,
    bool LauncherActive);

internal readonly record struct FloatingOwnerRuntimeSnapshot(
    bool OverlayVisible,
    bool ToolbarOwnerAlreadyOverlay,
    bool RollCallOwnerAlreadyOverlay,
    bool ImageManagerOwnerAlreadyOverlay);
