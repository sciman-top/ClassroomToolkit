using
System;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowDedupDefaults
{
    internal const int MinIntervalMs = 0;
    internal static readonly DateTime UnsetTimestampUtc = DateTime.MinValue;
}

internal static class FloatingInteractiveDedupIntervalDefaults
{
    internal const int DefaultMs = 90;
    internal const int InteractiveMs = 130;
}

/// <summary>
/// 浮层交互场景（ Overlay 可见且处于图片/白板模式）判定与间隔选择的单一公式源。
/// 各 retouch/dedup 策略此前各自内联同一份三元表达式，现统一委托到此。
/// </summary>

internal enum RetouchThrottleReason
{
    None = 0,
    IntervalDisabled = 1,
    FirstRetouch = 2,
    WithinThrottleWindow = 3,
    OutsideThrottleWindow = 4
}

internal readonly record struct RetouchThrottleDecision(
    bool ShouldAllow,
    RetouchThrottleReason Reason);

internal readonly record struct ExplicitForegroundRetouchRuntimeState(
    DateTime LastRetouchUtc)
{
    internal static ExplicitForegroundRetouchRuntimeState Default => new(
        LastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc);
}

/// <summary>
/// 时间戳+状态去重的共享判定结果。LauncherBubbleVisibleChanged 与 SurfaceZOrderDecision
/// 两个 DedupPolicy 的决策树同构，统一委托到 TimestampDedupCore 后各自映射回私有 Reason 枚举。
/// </summary>
internal enum TimestampDedupOutcome
{
    ApplyNoHistory = 0,
    ApplyDedupDisabledByInterval = 1,
    ApplyUnsetTimestamp = 2,
    SuppressDuplicateWithinWindow = 3,
    Apply = 4
}

internal static class TimestampDedupCore
{
    internal static TimestampDedupOutcome Resolve(
        bool hasHistory,
        bool timestampIsSet,
        bool isDuplicate,
        double elapsedMs,
        double minIntervalMs)
    {
        if (!hasHistory)
        {
            return TimestampDedupOutcome.ApplyNoHistory;
        }
        if (minIntervalMs <= WindowDedupDefaults.MinIntervalMs)
        {
            return TimestampDedupOutcome.ApplyDedupDisabledByInterval;
        }
        if (!timestampIsSet)
        {
            return TimestampDedupOutcome.ApplyUnsetTimestamp;
        }
        if (isDuplicate && elapsedMs < minIntervalMs)
        {
            return TimestampDedupOutcome.SuppressDuplicateWithinWindow;
        }
        return TimestampDedupOutcome.Apply;
    }
}

internal static class ExplicitForegroundRetouchStateUpdater
{
    internal static void MarkRetouched(
        ref ExplicitForegroundRetouchRuntimeState state,
        DateTime nowUtc)
    {
        state = new ExplicitForegroundRetouchRuntimeState(nowUtc);
    }
}

internal enum ForegroundExplicitRetouchThrottleReason
{
    None = 0,
    Throttled = 1
}

internal readonly record struct ForegroundExplicitRetouchThrottleDecision(
    bool ShouldAllowRetouch,
    ForegroundExplicitRetouchThrottleReason Reason);

internal static class WindowingDedupPolicies
{
    internal static bool IsInteractiveScene(bool overlayVisible, bool photoModeActive, bool whiteboardActive)
    {
        return overlayVisible && (photoModeActive || whiteboardActive);
    }

    internal static int ResolveMs(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive,
        int defaultMs,
        int interactiveMs)
    {
        return IsInteractiveScene(overlayVisible, photoModeActive, whiteboardActive)
            ? interactiveMs
            : defaultMs;
    }

    internal static RetouchThrottleDecision ResolveRetouchThrottle(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        if (minimumIntervalMs <= 0)
        {
            return new RetouchThrottleDecision(
                ShouldAllow: true,
                Reason: RetouchThrottleReason.IntervalDisabled);
        }
        if (lastRetouchUtc == WindowDedupDefaults.UnsetTimestampUtc)
        {
            return new RetouchThrottleDecision(
                ShouldAllow: true,
                Reason: RetouchThrottleReason.FirstRetouch);
        }

        var allow = (nowUtc - lastRetouchUtc).TotalMilliseconds >= minimumIntervalMs;
        return allow
            ? new RetouchThrottleDecision(
                ShouldAllow: true,
                Reason: RetouchThrottleReason.OutsideThrottleWindow)
            : new RetouchThrottleDecision(
                ShouldAllow: false,
                Reason: RetouchThrottleReason.WithinThrottleWindow);
    }

    internal static bool ShouldAllow(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return ResolveRetouchThrottle(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs).ShouldAllow;
    }

    internal static ForegroundExplicitRetouchThrottleDecision ResolveForegroundExplicitRetouchThrottle(
        ExplicitForegroundRetouchRuntimeState state,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return ResolveForegroundExplicitRetouchThrottle(
            state.LastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
    }

    internal static ForegroundExplicitRetouchThrottleDecision ResolveForegroundExplicitRetouchThrottle(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        var shouldAllow = WindowingDedupPolicies.ShouldAllow(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
        return shouldAllow
            ? new ForegroundExplicitRetouchThrottleDecision(
                ShouldAllowRetouch: true,
                Reason: ForegroundExplicitRetouchThrottleReason.None)
            : new ForegroundExplicitRetouchThrottleDecision(
                ShouldAllowRetouch: false,
                Reason: ForegroundExplicitRetouchThrottleReason.Throttled);
    }

    internal static bool ShouldAllowRetouch(
        ExplicitForegroundRetouchRuntimeState state,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return ResolveForegroundExplicitRetouchThrottle(
            state.LastRetouchUtc,
            nowUtc,
            minimumIntervalMs).ShouldAllowRetouch;
    }

    internal static bool ShouldAllowRetouch(
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return ResolveForegroundExplicitRetouchThrottle(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs).ShouldAllowRetouch;
    }
}
