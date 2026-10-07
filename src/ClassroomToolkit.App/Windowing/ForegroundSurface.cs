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
    ForegroundExplicitRetouchThrottleReason Reason);

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

        var retouchDecision = OverlayActivationRetouchPolicy.Resolve(
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

        if (OverlayActivationRetouchPolicy.ShouldUpdateLastRetouchUtc(retouchDecision))
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

        var throttleDecision = ForegroundExplicitRetouchThrottlePolicy.Resolve(
            state,
            nowUtc,
            minimumIntervalMs);
        if (!throttleDecision.ShouldAllowRetouch)
        {
            return new ExplicitForegroundRetouchExecutionResult(
                Applied: false,
                Reason: throttleDecision.Reason);
        }

        markRetouched(nowUtc);
        applySurfaceDecision(decision);
        return new ExplicitForegroundRetouchExecutionResult(
            Applied: true,
            Reason: throttleDecision.Reason);
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

internal enum ForegroundZOrderRetouchTrigger
{
    ToolbarInteraction = 0,
    ImageManagerActivated = 1,
    ImageManagerClosed = 2,
    ImageManagerStateChanged = 3,
    PhotoModeChanged = 4,
    PresentationFullscreenDetected = 5
}

internal enum ForegroundZOrderRetouchReason
{
    None = 0,
    ForceDisabledByDesign = 1,
    OverlayVisiblePresentation = 2,
    OverlayHiddenPresentation = 3
}

internal readonly record struct ForegroundZOrderRetouchDecision(
    bool ShouldForce,
    ForegroundZOrderRetouchReason Reason);

internal static class ForegroundZOrderRetouchPolicy
{
    internal static ForegroundZOrderRetouchDecision Resolve(
        ForegroundZOrderRetouchTrigger trigger,
        bool overlayVisible,
        bool photoModeActive = false,
        bool whiteboardActive = false)
    {
        _ = photoModeActive;
        _ = whiteboardActive;

        if (trigger == ForegroundZOrderRetouchTrigger.PresentationFullscreenDetected)
        {
            return overlayVisible
                ? new ForegroundZOrderRetouchDecision(
                    ShouldForce: true,
                    Reason: ForegroundZOrderRetouchReason.OverlayVisiblePresentation)
                : new ForegroundZOrderRetouchDecision(
                    ShouldForce: false,
                    Reason: ForegroundZOrderRetouchReason.OverlayHiddenPresentation);
        }

        return new ForegroundZOrderRetouchDecision(
            ShouldForce: false,
            Reason: ForegroundZOrderRetouchReason.ForceDisabledByDesign);
    }

    internal static bool ShouldForceOnToolbarInteraction(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.ToolbarInteraction,
            overlayVisible,
            photoModeActive,
            whiteboardActive).ShouldForce;
    }

    internal static bool ShouldForceOnImageManagerActivated(bool overlayVisible)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.ImageManagerActivated,
            overlayVisible).ShouldForce;
    }

    internal static bool ShouldForceOnImageManagerClosed(bool overlayVisible)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.ImageManagerClosed,
            overlayVisible).ShouldForce;
    }

    internal static bool ShouldForceOnImageManagerStateChanged(bool overlayVisible)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.ImageManagerStateChanged,
            overlayVisible).ShouldForce;
    }

    internal static bool ShouldForceOnPhotoModeChanged(bool photoModeActive)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.PhotoModeChanged,
            overlayVisible: false,
            photoModeActive: photoModeActive).ShouldForce;
    }

    internal static bool ShouldForceOnPresentationFullscreenDetected(bool overlayVisible)
    {
        return Resolve(
            ForegroundZOrderRetouchTrigger.PresentationFullscreenDetected,
            overlayVisible).ShouldForce;
    }
}
