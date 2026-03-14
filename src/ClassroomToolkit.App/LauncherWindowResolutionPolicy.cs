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
}
