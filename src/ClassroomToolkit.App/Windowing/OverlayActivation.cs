using
System.Windows.Input;
using
System.Windows;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal enum OverlayActivationReason
{
    None = 0,
    OverlayHidden = 1,
    SurfaceNotActivatable = 2,
    OverlayAlreadyActive = 3,
    BlockedByToolbar = 4,
    BlockedByRollCall = 5,
    BlockedByImageManager = 6,
    BlockedByLauncher = 7
}

internal readonly record struct OverlayActivationDecision(
    bool ShouldActivate,
    OverlayActivationReason Reason);

internal static class OverlayActivationPolicy
{
    internal static OverlayActivationDecision Resolve(
        bool overlayVisible,
        bool overlayShouldActivate,
        bool overlayActive,
        bool toolbarActive,
        bool imageManagerActive,
        bool rollCallActive,
        bool launcherActive)
    {
        if (!overlayVisible)
        {
            return new OverlayActivationDecision(
                ShouldActivate: false,
                Reason: OverlayActivationReason.OverlayHidden);
        }

        if (!overlayShouldActivate)
        {
            return new OverlayActivationDecision(
                ShouldActivate: false,
                Reason: OverlayActivationReason.SurfaceNotActivatable);
        }

        if (overlayActive)
        {
            return new OverlayActivationDecision(
                ShouldActivate: false,
                Reason: OverlayActivationReason.OverlayAlreadyActive);
        }

        var guardDecision = FloatingActivationGuardPolicy.Resolve(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));
        return guardDecision.IsBlocked
            ? new OverlayActivationDecision(
                ShouldActivate: false,
                Reason: guardDecision.Reason switch
                {
                    FloatingActivationGuardReason.ToolbarActive => OverlayActivationReason.BlockedByToolbar,
                    FloatingActivationGuardReason.RollCallActive => OverlayActivationReason.BlockedByRollCall,
                    FloatingActivationGuardReason.ImageManagerActive => OverlayActivationReason.BlockedByImageManager,
                    FloatingActivationGuardReason.LauncherActive => OverlayActivationReason.BlockedByLauncher,
                    _ => OverlayActivationReason.BlockedByToolbar
                })
            : new OverlayActivationDecision(
                ShouldActivate: true,
                Reason: OverlayActivationReason.None);
    }

    internal static bool ShouldActivate(
        bool overlayVisible,
        bool overlayShouldActivate,
        bool overlayActive,
        bool toolbarActive,
        bool imageManagerActive,
        bool rollCallActive,
        bool launcherActive)
    {
        return Resolve(
            overlayVisible,
            overlayShouldActivate,
            overlayActive,
            toolbarActive: toolbarActive,
            imageManagerActive: imageManagerActive,
            rollCallActive: rollCallActive,
            launcherActive: launcherActive).ShouldActivate;
    }
}

internal static class OverlayActivationDiagnosticsPolicy
{
    internal static string FormatRetouchSkipMessage(OverlayActivationRetouchReason reason)
    {
        return $"[OverlayActivation][Retouch] skip reason={reason}";
    }

    internal static string FormatSuppressionMessage(OverlayActivationSuppressionReason reason)
    {
        return $"[OverlayActivation][Suppression] apply reason={reason}";
    }
}

internal enum OverlayActivationSuppressionReason
{
    None = 0,
    SuppressionRequested = 1
}

internal readonly record struct OverlayActivationSuppressionDecision(
    bool ShouldSuppress,
    OverlayActivationSuppressionReason Reason);

internal static class OverlayActivationSuppressionPolicy
{
    internal static OverlayActivationSuppressionDecision Resolve(bool suppressNextOverlayActivatedZOrderApply)
    {
        return suppressNextOverlayActivatedZOrderApply
            ? new OverlayActivationSuppressionDecision(
                ShouldSuppress: true,
                Reason: OverlayActivationSuppressionReason.SuppressionRequested)
            : new OverlayActivationSuppressionDecision(
                ShouldSuppress: false,
                Reason: OverlayActivationSuppressionReason.None);
    }

    internal static bool ShouldSuppress(bool suppressNextOverlayActivatedZOrderApply)
    {
        return Resolve(suppressNextOverlayActivatedZOrderApply).ShouldSuppress;
    }
}

internal static class OverlayActivationSuppressionPolicyAdapter
{
    internal static FloatingWindowActivationPlan ApplySuppression(
        FloatingWindowActivationPlan plan,
        bool suppressOverlayActivation)
    {
        if (!suppressOverlayActivation)
        {
            return plan;
        }

        return plan with { ActivateOverlay = false };
    }
}

internal enum OverlayActivationSurfaceReason
{
    None = 0,
    OverlayHidden = 1,
    SurfaceNotSupported = 2
}

internal readonly record struct OverlayActivationSurfaceDecision(
    bool ShouldActivate,
    OverlayActivationSurfaceReason Reason);

internal static class OverlayActivationSurfacePolicy
{
    internal static OverlayActivationSurfaceDecision Resolve(bool overlayVisible, ZOrderSurface frontSurface)
    {
        if (!overlayVisible)
        {
            return new OverlayActivationSurfaceDecision(
                ShouldActivate: false,
                Reason: OverlayActivationSurfaceReason.OverlayHidden);
        }

        var supported = frontSurface is ZOrderSurface.PhotoFullscreen or ZOrderSurface.Whiteboard;
        return supported
            ? new OverlayActivationSurfaceDecision(
                ShouldActivate: true,
                Reason: OverlayActivationSurfaceReason.None)
            : new OverlayActivationSurfaceDecision(
                ShouldActivate: false,
                Reason: OverlayActivationSurfaceReason.SurfaceNotSupported);
    }

    internal static bool ShouldActivate(bool overlayVisible, ZOrderSurface frontSurface)
    {
        return Resolve(overlayVisible, frontSurface).ShouldActivate;
    }
}

internal enum OverlayActivationRetouchReason
{
    None = 0,
    NoApplyRequest = 1,
    Throttled = 2,
    Forced = 3
}

internal readonly record struct OverlayActivationRetouchDecision(
    bool ShouldApply,
    bool ShouldUpdateLastRetouchUtc,
    OverlayActivationRetouchReason Reason);

internal static class OverlayActivationRetouchPolicy
{
    internal static OverlayActivationRetouchDecision Resolve(
        SurfaceZOrderDecision decision,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        if (!decision.RequestZOrderApply)
        {
            return new OverlayActivationRetouchDecision(
                ShouldApply: false,
                ShouldUpdateLastRetouchUtc: false,
                Reason: OverlayActivationRetouchReason.NoApplyRequest);
        }

        if (decision.ForceEnforceZOrder)
        {
            return new OverlayActivationRetouchDecision(
                ShouldApply: true,
                ShouldUpdateLastRetouchUtc: false,
                Reason: OverlayActivationRetouchReason.Forced);
        }

        var shouldApply = WindowingDedupPolicies.ShouldAllowRetouch(
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs);
        return shouldApply
            ? new OverlayActivationRetouchDecision(
                ShouldApply: true,
                ShouldUpdateLastRetouchUtc: true,
                Reason: OverlayActivationRetouchReason.None)
            : new OverlayActivationRetouchDecision(
                ShouldApply: false,
                ShouldUpdateLastRetouchUtc: false,
                Reason: OverlayActivationRetouchReason.Throttled);
    }

    internal static bool ShouldApply(
        SurfaceZOrderDecision decision,
        DateTime lastRetouchUtc,
        DateTime nowUtc,
        int minimumIntervalMs)
    {
        return Resolve(
            decision,
            lastRetouchUtc,
            nowUtc,
            minimumIntervalMs).ShouldApply;
    }

    internal static bool ShouldUpdateLastRetouchUtc(
        SurfaceZOrderDecision decision,
        bool shouldApply)
    {
        return shouldApply && decision.RequestZOrderApply && !decision.ForceEnforceZOrder;
    }

    internal static bool ShouldUpdateLastRetouchUtc(OverlayActivationRetouchDecision decision)
    {
        return decision.ShouldUpdateLastRetouchUtc;
    }
}

internal readonly record struct OverlayActivatedRetouchRuntimeState(
    bool SuppressNextApply,
    DateTime LastRetouchUtc)
{
    internal static OverlayActivatedRetouchRuntimeState Default => new(
        SuppressNextApply: false,
        LastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc);
}

internal static class OverlayActivatedRetouchStateUpdater
{
    internal static void MarkSuppressNextApply(ref OverlayActivatedRetouchRuntimeState state)
    {
        state = state with { SuppressNextApply = true };
    }

    internal static bool TryConsumeSuppression(ref OverlayActivatedRetouchRuntimeState state)
    {
        if (!state.SuppressNextApply)
        {
            return false;
        }

        state = state with { SuppressNextApply = false };
        return true;
    }

    internal static void MarkRetouched(ref OverlayActivatedRetouchRuntimeState state, DateTime nowUtc)
    {
        state = state with { LastRetouchUtc = nowUtc };
    }
}

internal static class OverlayTopmostEnforcePolicy
{
    internal static bool ResolveForPhotoFullscreen(bool overlayCurrentlyTopmost)
    {
        // Keep fullscreen transitions stable: only force native z-order replay
        // when overlay is not topmost yet.
        return !overlayCurrentlyTopmost;
    }
}

internal readonly record struct OverlayFocusExecutionDecision(
    bool ShouldActivate,
    bool ShouldKeyboardFocus);

internal static class OverlayFocusExecutionExecutor
{
    internal static void Apply(
        Window? target,
        bool shouldActivate,
        bool shouldKeyboardFocus)
    {
        Apply(
            target,
            shouldActivate,
            shouldKeyboardFocus,
            (window, activate) => WindowActivationExecutor.TryActivate(window, activate),
            (element, focus) => WindowActivationExecutor.TryKeyboardFocus(element, focus));
    }

    internal static void Apply<TTarget>(
        TTarget? target,
        bool shouldActivate,
        bool shouldKeyboardFocus,
        Func<TTarget?, bool, bool> tryActivate,
        Func<TTarget?, bool, bool> tryKeyboardFocus)
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(tryActivate);
        ArgumentNullException.ThrowIfNull(tryKeyboardFocus);

        var decision = Resolve(shouldActivate, shouldKeyboardFocus);
        _ = SafeActionExecutionExecutor.TryExecute(() => tryActivate(target, decision.ShouldActivate));
        _ = SafeActionExecutionExecutor.TryExecute(() => tryKeyboardFocus(target, decision.ShouldKeyboardFocus));
    }

    internal static OverlayFocusExecutionDecision Resolve(
        bool shouldActivate,
        bool shouldKeyboardFocus)
    {
        return new OverlayFocusExecutionDecision(
            ShouldActivate: shouldActivate,
            ShouldKeyboardFocus: shouldKeyboardFocus);
    }
}

internal static class OverlayFullscreenBoundsRecoveryExecutor
{
    internal static void Apply(
        bool shouldRecover,
        Action<bool> normalizeWindowState,
        Action applyImmediateBounds,
        Action applyDeferredBounds)
    {
        ArgumentNullException.ThrowIfNull(normalizeWindowState);
        ArgumentNullException.ThrowIfNull(applyImmediateBounds);
        ArgumentNullException.ThrowIfNull(applyDeferredBounds);

        if (!shouldRecover)
        {
            return;
        }

        _ = SafeActionExecutionExecutor.TryExecute(() => normalizeWindowState(true));
        _ = SafeActionExecutionExecutor.TryExecute(applyImmediateBounds);
        _ = SafeActionExecutionExecutor.TryExecute(applyDeferredBounds);
    }
}
