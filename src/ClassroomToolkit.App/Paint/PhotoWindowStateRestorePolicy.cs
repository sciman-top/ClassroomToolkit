using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoWindowStateRestorePolicy
{
    internal static bool ShouldArmFullscreenRestore(bool photoFullscreen)
    {
        return photoFullscreen;
    }

    internal static bool ShouldRestoreFullscreen(bool pendingFullscreenRestore, WindowState windowState)
    {
        return pendingFullscreenRestore && windowState != WindowState.Minimized;
    }
}
