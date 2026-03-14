namespace ClassroomToolkit.App.Windowing;

internal static class LauncherWindowRuntimeSnapshotPolicy
{
    public static LauncherWindowRuntimeSnapshot Resolve(
        bool launcherMinimized,
        bool mainVisible,
        bool mainMinimized,
        bool mainActive,
        bool bubbleVisible,
        bool bubbleMinimized,
        bool bubbleActive)
    {
        var mainVisibleForTopmost = mainVisible && !mainMinimized;
        var bubbleVisibleForTopmost = bubbleVisible && !bubbleMinimized;
        var preferBubbleWindow = launcherMinimized;

        var selection = ResolveWindowSelection(
            preferBubbleWindow,
            mainVisibleForTopmost,
            bubbleVisibleForTopmost);
        var windowKind = selection.WindowKind;
        var visibleForTopmost = windowKind == LauncherWindowKind.Bubble
            ? bubbleVisibleForTopmost
            : mainVisibleForTopmost;
        var active = windowKind == LauncherWindowKind.Bubble
            ? bubbleVisibleForTopmost && bubbleActive
            : mainVisibleForTopmost && mainActive;

        return new LauncherWindowRuntimeSnapshot(
            VisibleForTopmost: visibleForTopmost,
            Active: active,
            WindowKind: windowKind,
            SelectionReason: selection.Reason);
    }

    private static (LauncherWindowKind WindowKind, LauncherWindowRuntimeSelectionReason Reason) ResolveWindowSelection(
        bool preferBubbleWindow,
        bool mainVisibleForTopmost,
        bool bubbleVisibleForTopmost)
    {
        if (preferBubbleWindow)
        {
            if (bubbleVisibleForTopmost)
            {
                return (LauncherWindowKind.Bubble, LauncherWindowRuntimeSelectionReason.PreferBubbleVisible);
            }

            if (!mainVisibleForTopmost)
            {
                return (LauncherWindowKind.Bubble, LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible);
            }

            return (LauncherWindowKind.Main, LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible);
        }

        if (mainVisibleForTopmost)
        {
            return (LauncherWindowKind.Main, LauncherWindowRuntimeSelectionReason.PreferMainVisible);
        }

        if (!bubbleVisibleForTopmost)
        {
            return (LauncherWindowKind.Main, LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible);
        }

        return (LauncherWindowKind.Bubble, LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible);
    }
}
