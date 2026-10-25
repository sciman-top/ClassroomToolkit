
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

    internal static bool ShouldEnableTransparent(bool hovering, bool paintAllowsTransparency)
    {
        return !hovering && paintAllowsTransparency;
    }

    internal static bool ShouldApplyStyle(bool transparentEnabled, bool? lastTransparentEnabled)
    {
        return lastTransparentEnabled != transparentEnabled;
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
        return transparentEnabled && !hoverTimerEnabled;
    }

    internal static bool ShouldStopHoverTimer(bool transparentEnabled, bool hoverTimerEnabled)
    {
        return !transparentEnabled && hoverTimerEnabled;
    }
}
