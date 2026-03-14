namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingWindowCoordinationSnapshot(
    FloatingWindowRuntimeSnapshot Runtime,
    LauncherWindowRuntimeSnapshot Launcher,
    FloatingTopmostVisibilitySnapshot TopmostVisibility,
    FloatingUtilityActivitySnapshot UtilityActivity,
    FloatingOwnerRuntimeSnapshot Owner);
