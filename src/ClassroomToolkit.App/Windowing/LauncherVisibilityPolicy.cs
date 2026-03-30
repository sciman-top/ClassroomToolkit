namespace ClassroomToolkit.App.Windowing;

internal enum LauncherTopmostVisibilityReason
{
    None = 0,
    MainVisible = 1,
    MainHiddenOrMinimized = 2,
    BubbleVisible = 3,
    BubbleHiddenOrMinimized = 4
}

internal readonly record struct LauncherTopmostVisibilityDecision(
    bool IsVisible,
    LauncherTopmostVisibilityReason Reason);

internal static class LauncherVisibilityPolicy
{
    internal static LauncherTopmostVisibilityDecision ResolveForTopmost(
        bool launcherMinimized,
        bool mainVisible,
        bool mainMinimized,
        bool bubbleVisible,
        bool bubbleMinimized)
    {
        if (launcherMinimized)
        {
            return bubbleVisible && !bubbleMinimized
                ? new LauncherTopmostVisibilityDecision(
                    IsVisible: true,
                    Reason: LauncherTopmostVisibilityReason.BubbleVisible)
                : new LauncherTopmostVisibilityDecision(
                    IsVisible: false,
                    Reason: LauncherTopmostVisibilityReason.BubbleHiddenOrMinimized);
        }

        return mainVisible && !mainMinimized
            ? new LauncherTopmostVisibilityDecision(
                IsVisible: true,
                Reason: LauncherTopmostVisibilityReason.MainVisible)
            : new LauncherTopmostVisibilityDecision(
                IsVisible: false,
                Reason: LauncherTopmostVisibilityReason.MainHiddenOrMinimized);
    }

    internal static bool IsVisibleForTopmost(
        bool launcherMinimized,
        bool mainVisible,
        bool mainMinimized,
        bool bubbleVisible,
        bool bubbleMinimized)
    {
        return ResolveForTopmost(
            launcherMinimized,
            mainVisible,
            mainMinimized,
            bubbleVisible,
            bubbleMinimized).IsVisible;
    }
}
