namespace ClassroomToolkit.App.Windowing;

internal static class LauncherWindowResolverPolicy
{
    internal static LauncherWindowKind Resolve(
        LauncherWindowKind preferredKind,
        bool bubbleExists,
        bool bubbleVisible,
        bool mainVisible)
    {
        if (preferredKind == LauncherWindowKind.Bubble && bubbleExists && bubbleVisible)
        {
            return LauncherWindowKind.Bubble;
        }

        if (preferredKind == LauncherWindowKind.Main && mainVisible)
        {
            return LauncherWindowKind.Main;
        }

        if (mainVisible)
        {
            return LauncherWindowKind.Main;
        }

        if (bubbleExists && bubbleVisible)
        {
            return LauncherWindowKind.Bubble;
        }

        // Keep a deterministic fallback even when both are transiently hidden.
        return preferredKind == LauncherWindowKind.Bubble && bubbleExists
            ? LauncherWindowKind.Bubble
            : LauncherWindowKind.Main;
    }
}
