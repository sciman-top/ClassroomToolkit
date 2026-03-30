namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingTopmostVisibilitySnapshot(
    bool ToolbarVisible,
    bool RollCallVisible,
    bool LauncherVisible,
    bool ImageManagerVisible,
    bool OverlayVisible);
