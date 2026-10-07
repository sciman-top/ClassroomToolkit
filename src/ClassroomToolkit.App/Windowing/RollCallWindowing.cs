
namespace ClassroomToolkit.App.Windowing;

internal readonly record struct RollCallVisibilityTransitionContext(
    bool RollCallVisible,
    bool RollCallActive,
    bool OverlayVisible);

internal readonly record struct RollCallVisibilityTransitionPlan(
    bool SyncOwnerToOverlay,
    bool ShowWindow,
    bool HideWindow,
    bool ActivateWindow,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal readonly record struct RollCallAuxOverlayTopmostPlan(
    bool PhotoOverlayTopmost,
    bool PhotoOverlayEnforceZOrder,
    bool GroupOverlayTopmost,
    bool GroupOverlayEnforceZOrder);

internal enum RollCallTransparencyReason
{
    None = 0,
    Enabled = 1,
    Hovering = 2,
    PaintModeDisallow = 3
}

internal readonly record struct RollCallTransparencyDecision(
    bool TransparentEnabled,
    RollCallTransparencyReason Reason);

internal enum RollCallTransparencyStyleApplyReason
{
    None = 0,
    StateChanged = 1,
    StateUnknown = 2,
    StateUnchanged = 3
}

internal readonly record struct RollCallTransparencyStyleApplyDecision(
    bool ShouldApplyStyle,
    RollCallTransparencyStyleApplyReason Reason);

internal enum RollCallHoverTimerReason
{
    None = 0,
    StartWhenTransparent = 1,
    StopWhenOpaque = 2,
    NoChange = 3
}

internal readonly record struct RollCallHoverTimerDecision(
    bool ShouldStart,
    bool ShouldStop,
    RollCallHoverTimerReason Reason);

internal static class RollCallWindowingPolicies
{
    internal static RollCallVisibilityTransitionPlan ResolveRollCallVisibilityTransition(RollCallVisibilityTransitionContext context)
    {
        return ResolveRollCallVisibilityTransition(
            rollCallVisible: context.RollCallVisible,
            rollCallActive: context.RollCallActive,
            overlayVisible: context.OverlayVisible);
    }

    internal static RollCallVisibilityTransitionPlan ResolveRollCallVisibilityTransition(
        bool rollCallVisible,
        bool rollCallActive,
        bool overlayVisible)
    {
        if (rollCallVisible)
        {
            return new RollCallVisibilityTransitionPlan(
                SyncOwnerToOverlay: false,
                ShowWindow: false,
                HideWindow: true,
                ActivateWindow: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: overlayVisible);
        }
        var activateWindowDecision = WindowExecutionPolicies.ResolveUserInitiatedWindowActivation(
            windowVisible: true,
            windowActive: rollCallActive);

        return new RollCallVisibilityTransitionPlan(
            SyncOwnerToOverlay: overlayVisible,
            ShowWindow: true,
            HideWindow: false,
            ActivateWindow: activateWindowDecision.ShouldActivateAfterShow,
            RequestZOrderApply: true,
            ForceEnforceZOrder: overlayVisible);
    }

    internal static RollCallAuxOverlayTopmostPlan ResolveRollCallAuxOverlayTopmost(
        bool photoOverlayVisible,
        bool groupOverlayVisible,
        bool enforceZOrder)
    {
        return new RollCallAuxOverlayTopmostPlan(
            PhotoOverlayTopmost: photoOverlayVisible,
            // 学生照片需要进入 topmost band，才能稳定压住普通焦点窗口；
            // 主窗口会随后重排工具条/启动器/点名窗口，让它们继续位于照片上方。
            PhotoOverlayEnforceZOrder: enforceZOrder,
            GroupOverlayTopmost: groupOverlayVisible,
            GroupOverlayEnforceZOrder: enforceZOrder);
    }

    internal static string FormatInitializationFailureMessage(string exceptionType, string message)
    {
        return $"[RollCallWindow] initialization-failed ex={exceptionType} msg={message}";
    }

    internal static string FormatDragMoveFailureMessage(string exceptionType, string message)
    {
        return $"[RollCallWindow] drag-move-failed ex={exceptionType} msg={message}";
    }

    internal static string FormatPhotoOverlayCloseFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] photo-overlay-close-failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatWindowLifecycleFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] window-lifecycle-failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatDialogShowFailureMessage(string dialogName, string exceptionType, string message)
    {
        return $"[RollCallWindow] dialog-show-failed dialog={dialogName} ex={exceptionType} msg={message}";
    }

    internal static string FormatGroupOverlayFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] group-overlay-failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatConfirmationFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] confirm-show-failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatRemoteHookDispatchFailureMessage(string operation, string exceptionType, string message)
    {
        return $"[RollCallWindow] remote-hook-dispatch-failed op={operation} ex={exceptionType} msg={message}";
    }

    internal static string FormatRemoteHookDispatchSkippedMessage(string operation, string reason)
    {
        return $"[RollCallWindow] remote-hook-dispatch-skipped op={operation} reason={reason}";
    }

    internal static RollCallTransparencyDecision ResolveTransparency(
        bool hovering,
        bool paintAllowsTransparency)
    {
        if (hovering)
        {
            return new RollCallTransparencyDecision(
                TransparentEnabled: false,
                Reason: RollCallTransparencyReason.Hovering);
        }

        if (!paintAllowsTransparency)
        {
            return new RollCallTransparencyDecision(
                TransparentEnabled: false,
                Reason: RollCallTransparencyReason.PaintModeDisallow);
        }

        return new RollCallTransparencyDecision(
            TransparentEnabled: true,
            Reason: RollCallTransparencyReason.Enabled);
    }

    internal static bool ShouldEnableTransparent(bool hovering, bool paintAllowsTransparency)
    {
        return ResolveTransparency(
            hovering,
            paintAllowsTransparency).TransparentEnabled;
    }

    internal static RollCallTransparencyStyleApplyDecision ResolveStyleApply(
        bool transparentEnabled,
        bool? lastTransparentEnabled)
    {
        if (!lastTransparentEnabled.HasValue)
        {
            return new RollCallTransparencyStyleApplyDecision(
                ShouldApplyStyle: true,
                Reason: RollCallTransparencyStyleApplyReason.StateUnknown);
        }

        var changed = lastTransparentEnabled.Value != transparentEnabled;
        return changed
            ? new RollCallTransparencyStyleApplyDecision(
                ShouldApplyStyle: true,
                Reason: RollCallTransparencyStyleApplyReason.StateChanged)
            : new RollCallTransparencyStyleApplyDecision(
                ShouldApplyStyle: false,
                Reason: RollCallTransparencyStyleApplyReason.StateUnchanged);
    }

    internal static bool ShouldApplyStyle(bool transparentEnabled, bool? lastTransparentEnabled)
    {
        return ResolveStyleApply(
            transparentEnabled,
            lastTransparentEnabled).ShouldApplyStyle;
    }

    internal static (int SetMask, int ClearMask) ResolveStyleMasks(bool transparentEnabled)
    {
        if (transparentEnabled)
        {
            return (WindowStyleBitMasks.WsExTransparent, 0);
        }

        return (0, WindowStyleBitMasks.WsExTransparent);
    }

    internal static bool ShouldStartHoverTimer(bool transparentEnabled, bool hoverTimerEnabled)
    {
        return ResolveHoverTimer(
            transparentEnabled,
            hoverTimerEnabled).ShouldStart;
    }

    internal static bool ShouldStopHoverTimer(bool transparentEnabled, bool hoverTimerEnabled)
    {
        return ResolveHoverTimer(
            transparentEnabled,
            hoverTimerEnabled).ShouldStop;
    }

    internal static RollCallHoverTimerDecision ResolveHoverTimer(
        bool transparentEnabled,
        bool hoverTimerEnabled)
    {
        if (transparentEnabled && !hoverTimerEnabled)
        {
            return new RollCallHoverTimerDecision(
                ShouldStart: true,
                ShouldStop: false,
                Reason: RollCallHoverTimerReason.StartWhenTransparent);
        }

        if (!transparentEnabled && hoverTimerEnabled)
        {
            return new RollCallHoverTimerDecision(
                ShouldStart: false,
                ShouldStop: true,
                Reason: RollCallHoverTimerReason.StopWhenOpaque);
        }

        return new RollCallHoverTimerDecision(
            ShouldStart: false,
            ShouldStop: false,
            Reason: RollCallHoverTimerReason.NoChange);
    }
}
