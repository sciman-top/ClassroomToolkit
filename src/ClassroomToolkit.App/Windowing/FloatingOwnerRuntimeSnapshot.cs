namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingOwnerRuntimeSnapshot(
    bool OverlayVisible,
    bool ToolbarOwnerAlreadyOverlay,
    bool RollCallOwnerAlreadyOverlay,
    bool ImageManagerOwnerAlreadyOverlay);
