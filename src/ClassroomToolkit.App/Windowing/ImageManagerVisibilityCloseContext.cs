namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ImageManagerVisibilityCloseContext(
    bool ImageManagerVisible,
    bool OwnerAlreadyOverlay);
