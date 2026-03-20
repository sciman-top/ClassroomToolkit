using System.Windows;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

internal static class LauncherWindowResolutionPolicy
{
    internal static bool ShouldUseBubbleWindow(
        LauncherWindowKind resolvedKind,
        bool bubbleWindowExists)
    {
        return resolvedKind == LauncherWindowKind.Bubble && bubbleWindowExists;
    }

    internal static Window ResolveWindow(
        LauncherWindowKind resolvedKind,
        Window mainWindow,
        Window? bubbleWindow)
    {
        ArgumentNullException.ThrowIfNull(mainWindow);

        return ShouldUseBubbleWindow(resolvedKind, bubbleWindow != null)
            ? bubbleWindow!
            : mainWindow;
    }
}
