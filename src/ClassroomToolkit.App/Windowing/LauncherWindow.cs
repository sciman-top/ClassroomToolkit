
namespace ClassroomToolkit.App.Windowing;

internal enum LauncherWindowKind
{
    Main,
    Bubble
}

internal readonly record struct LauncherWindowRuntimeSnapshot(
    bool VisibleForTopmost,
    bool Active,
    LauncherWindowKind WindowKind,
    LauncherWindowRuntimeSelectionReason SelectionReason);

internal enum LauncherWindowRuntimeSelectionReason
{
    None = 0,
    PreferMainVisible = 1,
    PreferBubbleVisible = 2,
    FallbackToMainBecauseBubbleNotVisible = 3,
    FallbackToBubbleBecauseMainNotVisible = 4
}

internal readonly record struct LauncherMinimizeTransitionContext(
    bool MainVisible,
    bool BubbleVisible);

internal readonly record struct LauncherRestoreTransitionContext(
    bool MainVisible,
    bool MainActive,
    bool BubbleVisible);

internal static class LauncherTopmostVisibilityHoldDefaults
{
    internal const int HoldMs = 180;
}

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

internal readonly record struct LauncherVisibilityTransitionPlan(
    bool ShowMainWindow,
    bool HideMainWindow,
    bool ShowBubbleWindow,
    bool HideBubbleWindow,
    bool ActivateMainWindow,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal enum LauncherVisibilityMinimizeReason
{
    None = 0,
    HideMainAndShowBubble = 1,
    HideMainOnly = 2,
    ShowBubbleOnly = 3,
    NoOp = 4
}

internal readonly record struct LauncherVisibilityMinimizeDecision(
    LauncherVisibilityTransitionPlan Plan,
    LauncherVisibilityMinimizeReason Reason);

internal enum LauncherVisibilityRestoreReason
{
    None = 0,
    ShowMainAndHideBubble = 1,
    ShowMainOnly = 2,
    HideBubbleOnly = 3,
    NoOp = 4
}

internal readonly record struct LauncherVisibilityRestoreDecision(
    LauncherVisibilityTransitionPlan Plan,
    LauncherVisibilityRestoreReason Reason);

internal static class LauncherWindowPolicies
{
    public static LauncherWindowRuntimeSnapshot ResolveRuntimeSnapshot(
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

    internal static LauncherWindowKind ResolveResolver(
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

    internal static bool ResolveVisibleForRepair(
        bool currentVisibleForTopmost,
        DateTime lastVisibleForTopmostUtc,
        DateTime nowUtc,
        int holdMs = LauncherTopmostVisibilityHoldDefaults.HoldMs)
    {
        if (currentVisibleForTopmost)
        {
            return true;
        }

        if (lastVisibleForTopmostUtc == MainWindowRuntimeDefaults.DefaultTimestampUtc)
        {
            return false;
        }

        var elapsedMs = (nowUtc - lastVisibleForTopmostUtc).TotalMilliseconds;
        return elapsedMs >= 0 && elapsedMs <= holdMs;
    }

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

    internal static LauncherVisibilityTransitionPlan ResolveMinimize(LauncherMinimizeTransitionContext context)
    {
        return ResolveMinimizeDecision(
            mainVisible: context.MainVisible,
            bubbleVisible: context.BubbleVisible).Plan;
    }

    internal static LauncherVisibilityMinimizeDecision ResolveMinimizeDecision(
        LauncherMinimizeTransitionContext context)
    {
        return ResolveMinimizeDecision(
            mainVisible: context.MainVisible,
            bubbleVisible: context.BubbleVisible);
    }

    internal static LauncherVisibilityMinimizeDecision ResolveMinimizeDecision(
        bool mainVisible,
        bool bubbleVisible)
    {
        var hideMainWindow = mainVisible;
        var showBubbleWindow = !bubbleVisible;
        var requestZOrderApply = hideMainWindow || showBubbleWindow;
        var reason = hideMainWindow && showBubbleWindow
            ? LauncherVisibilityMinimizeReason.HideMainAndShowBubble
            : hideMainWindow
                ? LauncherVisibilityMinimizeReason.HideMainOnly
                : showBubbleWindow
                    ? LauncherVisibilityMinimizeReason.ShowBubbleOnly
                    : LauncherVisibilityMinimizeReason.NoOp;
        return new LauncherVisibilityMinimizeDecision(
            Plan: new LauncherVisibilityTransitionPlan(
                ShowMainWindow: false,
                HideMainWindow: hideMainWindow,
                ShowBubbleWindow: showBubbleWindow,
                HideBubbleWindow: false,
                ActivateMainWindow: false,
                RequestZOrderApply: requestZOrderApply,
                ForceEnforceZOrder: requestZOrderApply),
            Reason: reason);
    }

    internal static LauncherVisibilityTransitionPlan ResolveMinimize(
        bool mainVisible,
        bool bubbleVisible)
    {
        return ResolveMinimizeDecision(mainVisible, bubbleVisible).Plan;
    }

    internal static LauncherVisibilityTransitionPlan ResolveRestore(LauncherRestoreTransitionContext context)
    {
        return ResolveRestoreDecision(
            mainVisible: context.MainVisible,
            mainActive: context.MainActive,
            bubbleVisible: context.BubbleVisible).Plan;
    }

    internal static LauncherVisibilityRestoreDecision ResolveRestoreDecision(LauncherRestoreTransitionContext context)
    {
        return ResolveRestoreDecision(
            mainVisible: context.MainVisible,
            mainActive: context.MainActive,
            bubbleVisible: context.BubbleVisible);
    }

    internal static LauncherVisibilityRestoreDecision ResolveRestoreDecision(
        bool mainVisible,
        bool mainActive,
        bool bubbleVisible)
    {
        var showMainWindow = !mainVisible;
        var hideBubbleWindow = bubbleVisible;
        var requestZOrderApply = showMainWindow || hideBubbleWindow;
        var activateMainWindowDecision = WindowExecutionPolicies.ResolveUserInitiatedWindowActivation(
            windowVisible: true,
            windowActive: mainActive);
        var reason = showMainWindow && hideBubbleWindow
            ? LauncherVisibilityRestoreReason.ShowMainAndHideBubble
            : showMainWindow
                ? LauncherVisibilityRestoreReason.ShowMainOnly
                : hideBubbleWindow
                    ? LauncherVisibilityRestoreReason.HideBubbleOnly
                    : LauncherVisibilityRestoreReason.NoOp;
        return new LauncherVisibilityRestoreDecision(
            Plan: new LauncherVisibilityTransitionPlan(
                ShowMainWindow: showMainWindow,
                HideMainWindow: false,
                ShowBubbleWindow: false,
                HideBubbleWindow: hideBubbleWindow,
                ActivateMainWindow: activateMainWindowDecision.ShouldActivateAfterShow,
                RequestZOrderApply: requestZOrderApply,
                ForceEnforceZOrder: requestZOrderApply),
            Reason: reason);
    }

    internal static LauncherVisibilityTransitionPlan ResolveRestore(
        bool mainVisible,
        bool mainActive,
        bool bubbleVisible)
    {
        return ResolveRestoreDecision(
            mainVisible,
            mainActive,
            bubbleVisible).Plan;
    }
}
