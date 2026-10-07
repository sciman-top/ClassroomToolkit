using
System;

namespace ClassroomToolkit.App.Windowing;

internal static class LauncherBubbleDiagnosticsPolicy
{
    internal static string FormatVisibleChangedGateSkipMessage(
        LauncherBubbleZOrderApplyGateReason reason,
        LauncherBubbleVisibleChangedApplyReason sourceReason = LauncherBubbleVisibleChangedApplyReason.None)
    {
        var message = $"[LauncherBubble][VisibleChangedGate] skip reason={reason}";
        if (sourceReason != LauncherBubbleVisibleChangedApplyReason.None)
        {
            message += $" source={sourceReason}";
        }

        return message;
    }

    internal static string FormatVisibleChangedDedupSkipMessage(LauncherBubbleVisibleChangedDedupReason reason)
    {
        return $"[LauncherBubble][VisibleChangedDedup] skip reason={reason}";
    }
}

internal readonly record struct LauncherBubbleVisibilityDecision(
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class LauncherBubbleVisibilityPolicy
{
    internal static LauncherBubbleVisibilityDecision Resolve(bool bubbleVisible)
    {
        return new LauncherBubbleVisibilityDecision(
            RequestZOrderApply: bubbleVisible,
            ForceEnforceZOrder: bubbleVisible);
    }
}

internal readonly record struct LauncherBubbleVisibilityRuntimeState(
    bool SuppressVisibleChangedApply,
    DateTime SuppressVisibleChangedUntilUtc,
    LauncherBubbleVisibleChangedRuntimeState VisibleChangedState)
{
    internal static LauncherBubbleVisibilityRuntimeState Default => new(
        SuppressVisibleChangedApply: false,
        SuppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
        VisibleChangedState: LauncherBubbleVisibleChangedRuntimeState.Default);
}

internal static class LauncherBubbleVisibilityStateUpdater
{
    internal static void MarkSuppressVisibleChangedApply(
        ref LauncherBubbleVisibilityRuntimeState state,
        bool suppress)
    {
        state = state with { SuppressVisibleChangedApply = suppress };
    }

    internal static void MarkVisibleChangedSuppressionCooldown(
        ref LauncherBubbleVisibilityRuntimeState state,
        DateTime nowUtc,
        int cooldownMs = LauncherBubbleVisibleChangedSuppressionDefaults.TransitionCooldownMs)
    {
        var untilUtc = cooldownMs <= 0
            ? WindowDedupDefaults.UnsetTimestampUtc
            : nowUtc.AddMilliseconds(cooldownMs);
        state = state with { SuppressVisibleChangedUntilUtc = untilUtc };
    }

    internal static void ApplyVisibleChangedDecision(
        ref LauncherBubbleVisibilityRuntimeState state,
        LauncherBubbleVisibleChangedDedupDecision decision)
    {
        state = state with
        {
            VisibleChangedState = new LauncherBubbleVisibleChangedRuntimeState(
                LastVisibleState: decision.LastVisibleState,
                LastEventUtc: decision.LastEventUtc)
        };
    }
}

internal enum LauncherBubbleVisibleChangedApplyReason
{
    None = 0,
    BubbleHidden = 1,
    VisibleChangedSuppressed = 2,
    CooldownActive = 3
}

internal readonly record struct LauncherBubbleVisibleChangedApplyDecision(
    bool ShouldApply,
    LauncherBubbleVisibleChangedApplyReason Reason);

internal static class LauncherBubbleVisibleChangedApplyPolicy
{
    internal static LauncherBubbleVisibleChangedApplyDecision Resolve(
        bool bubbleVisible,
        bool suppressVisibleChangedApply,
        DateTime suppressVisibleChangedUntilUtc,
        DateTime nowUtc)
    {
        if (!bubbleVisible)
        {
            return new LauncherBubbleVisibleChangedApplyDecision(
                ShouldApply: false,
                Reason: LauncherBubbleVisibleChangedApplyReason.BubbleHidden);
        }

        if (suppressVisibleChangedApply)
        {
            return new LauncherBubbleVisibleChangedApplyDecision(
                ShouldApply: false,
                Reason: LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed);
        }

        if (suppressVisibleChangedUntilUtc != WindowDedupDefaults.UnsetTimestampUtc
            && nowUtc < suppressVisibleChangedUntilUtc)
        {
            return new LauncherBubbleVisibleChangedApplyDecision(
                ShouldApply: false,
                Reason: LauncherBubbleVisibleChangedApplyReason.CooldownActive);
        }

        return new LauncherBubbleVisibleChangedApplyDecision(
            ShouldApply: true,
            Reason: LauncherBubbleVisibleChangedApplyReason.None);
    }

    internal static bool ShouldApplyZOrder(
        bool bubbleVisible,
        bool suppressVisibleChangedApply,
        DateTime suppressVisibleChangedUntilUtc,
        DateTime nowUtc)
    {
        return Resolve(
            bubbleVisible,
            suppressVisibleChangedApply,
            suppressVisibleChangedUntilUtc,
            nowUtc).ShouldApply;
    }
}

internal enum LauncherBubbleVisibleChangedDedupReason
{
    None = 0,
    DuplicateWithinWindow = 1,
    NoHistory = 2,
    DedupDisabledByInterval = 3,
    UnsetTimestamp = 4,
    Applied = 5
}

internal readonly record struct LauncherBubbleVisibleChangedDedupDecision(
    bool ShouldApply,
    LauncherBubbleVisibleChangedDedupReason Reason,
    bool? LastVisibleState,
    DateTime LastEventUtc);

internal static class LauncherBubbleVisibleChangedDedupPolicy
{
    internal static LauncherBubbleVisibleChangedDedupDecision Resolve(
        bool currentVisibleState,
        LauncherBubbleVisibleChangedRuntimeState state,
        DateTime nowUtc,
        int minIntervalMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs)
    {
        return Resolve(
            currentVisibleState,
            state.LastVisibleState,
            state.LastEventUtc,
            nowUtc,
            minIntervalMs);
    }

    internal static LauncherBubbleVisibleChangedDedupDecision Resolve(
        bool currentVisibleState,
        bool? lastVisibleState,
        DateTime lastEventUtc,
        DateTime nowUtc,
        int minIntervalMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs)
    {
        var outcome = TimestampDedupCore.Resolve(
            hasHistory: lastVisibleState.HasValue,
            timestampIsSet: lastEventUtc != WindowDedupDefaults.UnsetTimestampUtc,
            isDuplicate: lastVisibleState.GetValueOrDefault() == currentVisibleState,
            elapsedMs: (nowUtc - lastEventUtc).TotalMilliseconds,
            minIntervalMs: minIntervalMs);

        return outcome switch
        {
            TimestampDedupOutcome.SuppressDuplicateWithinWindow => new LauncherBubbleVisibleChangedDedupDecision(
                ShouldApply: false,
                Reason: LauncherBubbleVisibleChangedDedupReason.DuplicateWithinWindow,
                LastVisibleState: lastVisibleState,
                LastEventUtc: lastEventUtc),
            TimestampDedupOutcome.ApplyDedupDisabledByInterval => new LauncherBubbleVisibleChangedDedupDecision(
                ShouldApply: true,
                Reason: LauncherBubbleVisibleChangedDedupReason.DedupDisabledByInterval,
                LastVisibleState: currentVisibleState,
                LastEventUtc: nowUtc),
            TimestampDedupOutcome.ApplyUnsetTimestamp => new LauncherBubbleVisibleChangedDedupDecision(
                ShouldApply: true,
                Reason: LauncherBubbleVisibleChangedDedupReason.UnsetTimestamp,
                LastVisibleState: currentVisibleState,
                LastEventUtc: nowUtc),
            TimestampDedupOutcome.ApplyNoHistory => new LauncherBubbleVisibleChangedDedupDecision(
                ShouldApply: true,
                Reason: LauncherBubbleVisibleChangedDedupReason.NoHistory,
                LastVisibleState: currentVisibleState,
                LastEventUtc: nowUtc),
            _ => new LauncherBubbleVisibleChangedDedupDecision(
                ShouldApply: true,
                Reason: LauncherBubbleVisibleChangedDedupReason.Applied,
                LastVisibleState: currentVisibleState,
                LastEventUtc: nowUtc)
        };
    }
}

internal static class LauncherBubbleVisibleChangedDedupIntervalPolicy
{
    internal static int ResolveMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        int defaultMs = FloatingInteractiveDedupIntervalDefaults.DefaultMs,
        int interactiveMs = FloatingInteractiveDedupIntervalDefaults.InteractiveMs)
    {
        return WindowingDedupPolicies.ResolveMs(
            overlayVisible,
            photoModeActive,
            whiteboardActive,
            defaultMs,
            interactiveMs);
    }
}

internal readonly record struct LauncherBubbleVisibleChangedRuntimeState(
    bool? LastVisibleState,
    DateTime LastEventUtc)
{
    internal static LauncherBubbleVisibleChangedRuntimeState Default => new(
        LastVisibleState: null,
        LastEventUtc: WindowDedupDefaults.UnsetTimestampUtc);
}

internal static class LauncherBubbleVisibleChangedSuppressionPolicy
{
    internal static int ResolveCooldownMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        int defaultMs = LauncherBubbleVisibleChangedSuppressionDefaults.TransitionCooldownMs,
        int interactiveMs = LauncherBubbleVisibleChangedSuppressionDefaults.InteractiveTransitionCooldownMs)
    {
        return WindowingDedupPolicies.ResolveMs(
            overlayVisible,
            photoModeActive,
            whiteboardActive,
            defaultMs,
            interactiveMs);
    }
}

internal static class LauncherBubbleVisibleChangedSuppressionDefaults
{
    internal const int TransitionCooldownMs = 180;
    internal const int InteractiveTransitionCooldownMs = 260;
}

internal enum LauncherBubbleZOrderApplyGateReason
{
    None = 0,
    AppClosing = 1,
    BubbleWindowMissing = 2,
    BubbleHidden = 3,
    VisibleChangedSuppressed = 4,
    CooldownActive = 5
}

internal readonly record struct LauncherBubbleZOrderApplyGateDecision(
    bool ShouldApply,
    LauncherBubbleZOrderApplyGateReason Reason,
    LauncherBubbleVisibleChangedApplyReason VisibleChangedReason);

internal static class LauncherBubbleZOrderApplyGatePolicy
{
    internal static LauncherBubbleZOrderApplyGateDecision Resolve(
        bool bubbleVisible,
        bool suppressVisibleChangedApply,
        DateTime suppressVisibleChangedUntilUtc,
        DateTime nowUtc,
        bool appClosing,
        bool bubbleWindowExists)
    {
        if (appClosing)
        {
            return new LauncherBubbleZOrderApplyGateDecision(
                ShouldApply: false,
                Reason: LauncherBubbleZOrderApplyGateReason.AppClosing,
                VisibleChangedReason: LauncherBubbleVisibleChangedApplyReason.None);
        }

        if (!bubbleWindowExists)
        {
            return new LauncherBubbleZOrderApplyGateDecision(
                ShouldApply: false,
                Reason: LauncherBubbleZOrderApplyGateReason.BubbleWindowMissing,
                VisibleChangedReason: LauncherBubbleVisibleChangedApplyReason.None);
        }

        var visibleChangedDecision = LauncherBubbleVisibleChangedApplyPolicy.Resolve(
            bubbleVisible,
            suppressVisibleChangedApply,
            suppressVisibleChangedUntilUtc,
            nowUtc);
        return visibleChangedDecision.ShouldApply
            ? new LauncherBubbleZOrderApplyGateDecision(
                ShouldApply: true,
                Reason: LauncherBubbleZOrderApplyGateReason.None,
                VisibleChangedReason: LauncherBubbleVisibleChangedApplyReason.None)
            : new LauncherBubbleZOrderApplyGateDecision(
                ShouldApply: false,
                Reason: visibleChangedDecision.Reason switch
                {
                    LauncherBubbleVisibleChangedApplyReason.BubbleHidden => LauncherBubbleZOrderApplyGateReason.BubbleHidden,
                    LauncherBubbleVisibleChangedApplyReason.VisibleChangedSuppressed => LauncherBubbleZOrderApplyGateReason.VisibleChangedSuppressed,
                    LauncherBubbleVisibleChangedApplyReason.CooldownActive => LauncherBubbleZOrderApplyGateReason.CooldownActive,
                    _ => LauncherBubbleZOrderApplyGateReason.CooldownActive
                },
                VisibleChangedReason: visibleChangedDecision.Reason);
    }

    internal static bool ShouldApply(
        bool bubbleVisible,
        bool suppressVisibleChangedApply,
        DateTime suppressVisibleChangedUntilUtc,
        DateTime nowUtc,
        bool appClosing,
        bool bubbleWindowExists)
    {
        return Resolve(
            bubbleVisible,
            suppressVisibleChangedApply,
            suppressVisibleChangedUntilUtc,
            nowUtc,
            appClosing,
            bubbleWindowExists).ShouldApply;
    }
}
