namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ToolbarInteractionRetouchSnapshot(
    bool OverlayVisible,
    bool PhotoModeActive,
    bool WhiteboardActive,
    bool ToolbarVisible,
    bool ToolbarTopmost,
    bool RollCallVisible,
    bool RollCallTopmost,
    bool LauncherVisible,
    bool LauncherTopmost);
