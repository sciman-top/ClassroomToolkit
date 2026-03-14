namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingOwnerBindingContext(
    bool OverlayVisible,
    bool OwnerAlreadyOverlay);
