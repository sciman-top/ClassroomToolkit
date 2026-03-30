namespace ClassroomToolkit.App.Windowing;

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

internal static class LauncherVisibilityTransitionPolicy
{
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
        var activateMainWindowDecision = UserInitiatedWindowActivationPolicy.Resolve(
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
