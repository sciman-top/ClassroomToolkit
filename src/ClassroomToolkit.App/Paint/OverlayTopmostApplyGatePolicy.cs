using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class OverlayTopmostApplyGatePolicy
{
    internal static bool ShouldApply(bool overlayVisible, WindowState windowState)
    {
        return overlayVisible && windowState != WindowState.Minimized;
    }
}
