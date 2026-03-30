namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingUtilityActivitySnapshot(
    bool ToolbarActive,
    bool RollCallActive,
    bool ImageManagerActive,
    bool LauncherActive);
