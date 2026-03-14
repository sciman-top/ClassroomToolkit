namespace ClassroomToolkit.App.Windowing;

internal readonly record struct PaintWindowVisibilityShowContext(
    bool OverlayVisible,
    bool ToolbarExists,
    bool ToolbarOwnerAlreadyOverlay);
