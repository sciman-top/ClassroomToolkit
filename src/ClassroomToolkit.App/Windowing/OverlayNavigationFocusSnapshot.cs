namespace ClassroomToolkit.App.Windowing;

internal readonly record struct OverlayNavigationFocusSnapshot(
    bool OverlayVisible,
    bool OverlayActive,
    FloatingUtilityActivitySnapshot UtilityActivity);
