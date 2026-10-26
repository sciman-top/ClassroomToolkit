using
System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ForegroundSurfaceActivityState(
    bool OverlayExists,
    bool PhotoModeActive,
    bool WhiteboardActive);

internal enum ForegroundSurfaceTransitionKind
{
    OverlayActivated = 0,
    ExplicitForeground = 1
}

internal static class ForegroundSurfaceDecisionFactory
{
    internal static SurfaceZOrderDecision NoTouch(bool requestZOrderApply)
    {
        return new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: requestZOrderApply,
            ForceEnforceZOrder: false);
    }

    internal static SurfaceZOrderDecision Touch(ZOrderSurface surface)
    {
        return new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: surface,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);
    }

    internal static SurfaceZOrderDecision ExplicitForeground(bool overlayExists, ZOrderSurface surface)
    {
        return new SurfaceZOrderDecision(
            ShouldTouchSurface: overlayExists && surface != ZOrderSurface.None,
            Surface: overlayExists ? surface : ZOrderSurface.None,
            RequestZOrderApply: overlayExists,
            ForceEnforceZOrder: overlayExists);
    }
}

internal readonly record struct OverlayActivationRetouchExecutionResult(
    bool Applied,
    bool SuppressionConsumed,
    OverlayActivationRetouchReason Reason);

internal readonly record struct ExplicitForegroundRetouchExecutionResult(
    bool Applied,
    string Reason);

internal static class ForegroundSurfaceRetouchCoordinator
{
    internal static OverlayActivationRetouchExecutionResult ApplyOverlayActivated(
        bool suppressionConsumed,
        SurfaceZOrderDecision decision,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs,
        Action<DateTime> markRetouched,
        Action<SurfaceZOrderDecision> applySurfaceDecision)
    {
        ArgumentNullException.ThrowIfNull(markRetouched);
        ArgumentNullException.ThrowIfNull(applySurfaceDecision);

        if (suppressionConsumed)
        {
            return new OverlayActivationRetouchExecutionResult(
                Applied: false,
                SuppressionConsumed: true,
                Reason: OverlayActivationRetouchReason.NoApplyRequest);
        }

        var retouchDecision = OverlayActivationPolicies.ResolveRetouch(
            decision,
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
        if (!retouchDecision.ShouldApply)
        {
            return new OverlayActivationRetouchExecutionResult(
                Applied: false,
                SuppressionConsumed: false,
                Reason: retouchDecision.Reason);
        }

        if (OverlayActivationPolicies.ShouldUpdateLastRetouchUtc(retouchDecision))
        {
            markRetouched(nowUtc);
        }

        applySurfaceDecision(decision);
        return new OverlayActivationRetouchExecutionResult(
            Applied: true,
            SuppressionConsumed: false,
            Reason: retouchDecision.Reason);
    }

    internal static ExplicitForegroundRetouchExecutionResult ApplyExplicitForeground(
        ExplicitForegroundRetouchRuntimeState state,
        DateTime nowUtc,
        int minimumIntervalMs,
        SurfaceZOrderDecision decision,
        Action<DateTime> markRetouched,
        Action<SurfaceZOrderDecision> applySurfaceDecision)
    {
        ArgumentNullException.ThrowIfNull(markRetouched);
        ArgumentNullException.ThrowIfNull(applySurfaceDecision);

        var throttleDecision = WindowingDedupPolicies.ResolveRetouchThrottle(
            state.LastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
        if (!throttleDecision.ShouldAllow)
        {
            return new ExplicitForegroundRetouchExecutionResult(
                Applied: false,
                Reason: throttleDecision.Reason.ToString());
        }

        markRetouched(nowUtc);
        applySurfaceDecision(decision);
        return new ExplicitForegroundRetouchExecutionResult(
            Applied: true,
            Reason: throttleDecision.Reason.ToString());
    }
}

internal static class ForegroundSurfaceTransitionPolicy
{
    internal static SurfaceZOrderDecision Resolve(
        ForegroundSurfaceTransitionKind kind,
        bool suppressNextApply,
        ForegroundSurfaceActivityState activityState,
        ZOrderSurface surface)
    {
        return Resolve(
            kind,
            suppressNextApply,
            activityState.OverlayExists,
            activityState.PhotoModeActive,
            activityState.WhiteboardActive,
            surface);
    }

    internal static SurfaceZOrderDecision Resolve(
        ForegroundSurfaceTransitionKind kind,
        bool suppressNextApply,
        bool overlayExists,
        bool photoModeActive,
        bool whiteboardActive,
        ZOrderSurface surface)
    {
        return kind switch
        {
            ForegroundSurfaceTransitionKind.OverlayActivated => ResolveOverlayActivated(
                suppressNextApply,
                overlayExists,
                photoModeActive,
                whiteboardActive),
            ForegroundSurfaceTransitionKind.ExplicitForeground => ResolveExplicitForeground(
                overlayExists,
                surface),
            _ => ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: false)
        };
    }

    private static SurfaceZOrderDecision ResolveOverlayActivated(
        bool suppressNextApply,
        bool overlayExists,
        bool photoModeActive,
        bool whiteboardActive)
    {
        if (suppressNextApply || !overlayExists)
        {
            return ForegroundSurfaceDecisionFactory.NoTouch(
                requestZOrderApply: !suppressNextApply && overlayExists);
        }

        if (photoModeActive)
        {
            var decision = ForegroundSurfaceDecisionFactory.Touch(ZOrderSurface.PhotoFullscreen);
            return decision with { ForceEnforceZOrder = true };
        }

        if (whiteboardActive)
        {
            var decision = ForegroundSurfaceDecisionFactory.Touch(ZOrderSurface.Whiteboard);
            return decision with { ForceEnforceZOrder = true };
        }

        return ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: true);
    }

    private static SurfaceZOrderDecision ResolveExplicitForeground(
        bool overlayExists,
        ZOrderSurface surface)
    {
        return ForegroundSurfaceDecisionFactory.ExplicitForeground(overlayExists, surface);
    }
}

internal static class ForegroundZOrderRetouchPolicy
{
    /// <summary>
    /// 仅放映全屏检测按设计允许强制 z-order（覆盖层可见时）；工具栏交互、图库与照片模式
    /// 的强制升级一律禁用，由常规 retouch 与 topmost 漂移修复路径处理。
    /// </summary>
    internal static bool ShouldForceOnPresentationFullscreenDetected(bool overlayVisible)
    {
        return overlayVisible;
    }
}
