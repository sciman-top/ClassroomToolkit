using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ImageManagerVisibilityOpenContext(
    bool OverlayVisible,
    bool ImageManagerVisible,
    WindowState ImageManagerWindowState);
