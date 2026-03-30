using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ImageManagerStateChangeContext(
    bool ImageManagerExists,
    WindowState ImageManagerWindowState,
    bool OverlayVisible,
    WindowState OverlayWindowState)
{
    internal bool ImageManagerMinimized => ImageManagerWindowState == WindowState.Minimized;
    internal bool OverlayMinimized => OverlayWindowState == WindowState.Minimized;
}
