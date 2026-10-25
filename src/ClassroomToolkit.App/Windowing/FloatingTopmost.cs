using
ClassroomToolkit.App.Session;
using
System.Threading;
using
System.Windows;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostDialogSuppressionState
{
    private static int _suppressionDepth;

    internal static bool IsSuppressed => Volatile.Read(ref _suppressionDepth) > 0;

    internal static IDisposable Enter()
    {
        Interlocked.Increment(ref _suppressionDepth);
        return InteropAdapterScope.Create(() =>
        {
            var depth = Interlocked.Decrement(ref _suppressionDepth);
            if (depth >= 0)
            {
                return;
            }

            Interlocked.Exchange(ref _suppressionDepth, 0);
        });
    }
}

internal static class FloatingTopmostDriftRepairExecutor
{
    internal static void Apply(
        FloatingTopmostDriftRepairPlan plan,
        Window? toolbarWindow,
        Window? rollCallWindow,
        Window? launcherWindow,
        bool enforceZOrder = false,
        Action<Exception>? onFailure = null)
    {
        Apply(
            plan,
            toolbarWindow,
            rollCallWindow,
            launcherWindow,
            enforceZOrder,
            WindowTopmostExecutor.ApplyNoActivate,
            onFailure);
    }

    internal static void Apply(
        FloatingTopmostDriftRepairPlan plan,
        Window? toolbarWindow,
        Window? rollCallWindow,
        Window? launcherWindow,
        bool enforceZOrder,
        Action<Window?, bool, bool> applyTopmostNoActivate,
        Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(applyTopmostNoActivate);

        if (plan.RepairToolbar)
        {
            TryApplyTopmost(
                toolbarWindow,
                enforceZOrder,
                applyTopmostNoActivate,
                onFailure);
        }

        if (plan.RepairRollCall)
        {
            TryApplyTopmost(
                rollCallWindow,
                enforceZOrder,
                applyTopmostNoActivate,
                onFailure);
        }

        if (plan.RepairLauncher)
        {
            TryApplyTopmost(
                launcherWindow,
                enforceZOrder,
                applyTopmostNoActivate,
                onFailure);
        }
    }

    private static void TryApplyTopmost(
        Window? window,
        bool enforceZOrder,
        Action<Window?, bool, bool> applyTopmostNoActivate,
        Action<Exception>? onFailure)
    {
        _ = SafeActionExecutionExecutor.TryExecute(
            () => applyTopmostNoActivate(window, true, enforceZOrder),
            onFailure: onFailure);
    }
}

internal readonly record struct FloatingTopmostDriftRepairPlan(
    bool RepairToolbar,
    bool RepairRollCall,
    bool RepairLauncher);

internal readonly record struct FloatingTopmostExecutionPlan(
    bool ToolbarTopmost,
    bool RollCallTopmost,
    bool LauncherTopmost,
    bool ImageManagerTopmost,
    bool EnforceZOrder);

internal static class FloatingTopmostExecutionExecutor
{
    internal static void Apply(
        FloatingTopmostExecutionPlan plan,
        Window? toolbarWindow,
        Window? rollCallWindow,
        Window? launcherWindow,
        Window? imageManagerWindow)
    {
        Apply(
            plan,
            toolbarWindow,
            rollCallWindow,
            launcherWindow,
            imageManagerWindow,
            WindowTopmostExecutor.ApplyNoActivate);
    }

    internal static void Apply(
        FloatingTopmostExecutionPlan plan,
        Window? toolbarWindow,
        Window? rollCallWindow,
        Window? launcherWindow,
        Window? imageManagerWindow,
        Action<Window?, bool, bool> applyTopmostNoActivate)
    {
        ArgumentNullException.ThrowIfNull(applyTopmostNoActivate);

        TryApply(toolbarWindow, plan.ToolbarTopmost, plan.EnforceZOrder, applyTopmostNoActivate);
        TryApply(rollCallWindow, plan.RollCallTopmost, plan.EnforceZOrder, applyTopmostNoActivate);
        TryApply(launcherWindow, plan.LauncherTopmost, plan.EnforceZOrder, applyTopmostNoActivate);
        TryApply(imageManagerWindow, plan.ImageManagerTopmost, plan.EnforceZOrder, applyTopmostNoActivate);
    }

    private static void TryApply(
        Window? window,
        bool topmost,
        bool enforceZOrder,
        Action<Window?, bool, bool> applyTopmostNoActivate)
    {
        _ = SafeActionExecutionExecutor.TryExecute(() => applyTopmostNoActivate(window, topmost, enforceZOrder));
    }
}

internal readonly record struct FloatingTopmostPlan(
    bool ToolbarTopmost,
    bool RollCallTopmost,
    bool LauncherTopmost,
    bool ImageManagerTopmost,
    bool OverlayShouldActivate);

internal static class FloatingTopmostPolicies
{
    private readonly record struct TopmostPlanSnapshot(
        bool ToolbarTopmost,
        bool RollCallTopmost,
        bool LauncherTopmost,
        bool ImageManagerTopmost);

    internal static bool ShouldEnforceZOrder(
        ZOrderSurface? lastFrontSurface,
        ZOrderSurface currentFrontSurface,
        FloatingTopmostPlan? lastPlan,
        FloatingTopmostPlan currentPlan,
        bool forceEnforceZOrder = false)
    {
        if (forceEnforceZOrder)
        {
            return true;
        }

        if (!lastFrontSurface.HasValue || !lastPlan.HasValue)
        {
            return true;
        }

        if (lastFrontSurface.Value != currentFrontSurface)
        {
            return true;
        }

        var lastTopmost = ToTopmostPlan(lastPlan.Value);
        var currentTopmost = ToTopmostPlan(currentPlan);
        if (lastTopmost != currentTopmost)
        {
            return true;
        }

        if (ShouldRetouchLauncherOnInteractiveSurface(currentFrontSurface, currentPlan))
        {
            return true;
        }

        return false;
    }

    private static TopmostPlanSnapshot ToTopmostPlan(FloatingTopmostPlan plan)
        => new(
            plan.ToolbarTopmost,
            plan.RollCallTopmost,
            plan.LauncherTopmost,
            plan.ImageManagerTopmost);

    private static bool ShouldRetouchLauncherOnInteractiveSurface(
        ZOrderSurface frontSurface,
        FloatingTopmostPlan plan)
    {
        if (!plan.LauncherTopmost)
        {
            return false;
        }

        // Photo mode already has dedicated foreground/watchdog retouch gating; forcing
        // launcher retouch on every photo z-order request can cause visible flicker
        // when floating utility windows overlap the photo content.
        return frontSurface is ZOrderSurface.PresentationFullscreen
            or ZOrderSurface.Whiteboard;
    }

    internal static bool HasDrift(ToolbarInteractionRetouchSnapshot snapshot)
    {
        return (snapshot.ToolbarVisible && !snapshot.ToolbarTopmost)
            || (snapshot.RollCallVisible && !snapshot.RollCallTopmost)
            || (snapshot.LauncherVisible && !snapshot.LauncherTopmost);
    }

    /// <summary>
    /// Toolbar-interaction retouch deliberately never escalates to forced z-order;
    /// photo/fullscreen surfaces own that escalation elsewhere.
    /// </summary>
    internal static bool ShouldForceEnforce(ToolbarInteractionRetouchSnapshot snapshot)
    {
        _ = snapshot;
        return false;
    }

    internal static bool ResolveDriftRepairEnforce(ToolbarInteractionRetouchSnapshot snapshot, ToolbarInteractionRetouchTrigger trigger)
    {
        if (trigger != ToolbarInteractionRetouchTrigger.Activated)
        {
            return false;
        }

        if (!WindowingDedupPolicies.IsInteractiveScene(
                snapshot.OverlayVisible,
                snapshot.PhotoModeActive,
                snapshot.WhiteboardActive))
        {
            return false;
        }

        return snapshot.LauncherVisible && !snapshot.LauncherTopmost;
    }

    internal static FloatingTopmostDriftRepairPlan ResolveDriftRepair(ToolbarInteractionRetouchSnapshot snapshot)
    {
        return new FloatingTopmostDriftRepairPlan(
            RepairToolbar: snapshot.ToolbarVisible && !snapshot.ToolbarTopmost,
            RepairRollCall: snapshot.RollCallVisible && !snapshot.RollCallTopmost,
            RepairLauncher: snapshot.LauncherVisible && !snapshot.LauncherTopmost);
    }

    internal static FloatingTopmostPlan ResolvePlan(
        ZOrderSurface frontSurface,
        FloatingTopmostVisibilitySnapshot snapshot)
    {
        return ResolvePlan(
            frontSurface,
            snapshot.ToolbarVisible,
            snapshot.RollCallVisible,
            snapshot.LauncherVisible,
            snapshot.ImageManagerVisible,
            snapshot.OverlayVisible);
    }

    internal static FloatingTopmostPlan ResolvePlan(
        ZOrderSurface frontSurface,
        bool toolbarVisible,
        bool rollCallVisible,
        bool launcherVisible,
        bool imageManagerVisible,
        bool overlayVisible)
    {
        var imageManagerTopmostDecision = ImageManagerWindowingPolicies.ResolveImageManagerTopmost(imageManagerVisible, frontSurface);
        var overlayActivationSurfaceDecision = OverlayActivationPolicies.ResolveSurface(overlayVisible, frontSurface);
        var overlayShouldActivate = overlayActivationSurfaceDecision.ShouldActivate
            && !ShouldSuppressOverlayActivationOnPhotoSurface(
                frontSurface,
                toolbarVisible,
                rollCallVisible,
                launcherVisible);

        return new FloatingTopmostPlan(
            ToolbarTopmost: toolbarVisible,
            RollCallTopmost: rollCallVisible,
            LauncherTopmost: launcherVisible,
            ImageManagerTopmost: imageManagerTopmostDecision.ShouldApply,
            OverlayShouldActivate: overlayShouldActivate);
    }

    private static bool ShouldSuppressOverlayActivationOnPhotoSurface(
        ZOrderSurface frontSurface,
        bool toolbarVisible,
        bool rollCallVisible,
        bool launcherVisible)
    {
        if (frontSurface != ZOrderSurface.PhotoFullscreen)
        {
            return false;
        }

        return toolbarVisible || rollCallVisible || launcherVisible;
    }

    internal static bool ShouldEnsureFloatingOnTransition(UiSessionTransition transition)
    {
        return transition.Previous.OverlayTopmostRequired != transition.Current.OverlayTopmostRequired
            && transition.Current.OverlayTopmostRequired;
    }

    private const int IntervalMs = 700;

    internal static int ResolveIntervalMs() => IntervalMs;

    internal static bool ShouldForceRetouch(
        bool toolbarVisible,
        bool rollCallVisible,
        bool launcherVisible,
        bool imageManagerVisible,
        bool rollCallAuxOverlayVisible,
        bool photoModeActive)
    {
        if (photoModeActive)
        {
            return false;
        }

        return toolbarVisible
            || rollCallVisible
            || launcherVisible
            || imageManagerVisible
            || rollCallAuxOverlayVisible;
    }
}
