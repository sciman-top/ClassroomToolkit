using
ClassroomToolkit.App.Session;
using
System.Threading;
using
System.Windows;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostApplyPolicy
{
    private readonly record struct TopmostPlanSnapshot(
        bool ToolbarTopmost,
        bool RollCallTopmost,
        bool LauncherTopmost,
        bool ImageManagerTopmost);

    internal enum FloatingTopmostApplyReason
    {
        None = 0,
        ForceRequested = 1,
        MissingLastState = 2,
        FrontSurfaceChanged = 3,
        TopmostPlanChanged = 4,
        Unchanged = 5,
        LauncherInteractiveRetouch = 6
    }

    internal readonly record struct FloatingTopmostApplyDecision(
        bool ShouldEnforceZOrder,
        FloatingTopmostApplyReason Reason);

    internal static FloatingTopmostApplyDecision Resolve(
        ZOrderSurface? lastFrontSurface,
        ZOrderSurface currentFrontSurface,
        FloatingTopmostPlan? lastPlan,
        FloatingTopmostPlan currentPlan,
        bool forceEnforceZOrder = false)
    {
        if (forceEnforceZOrder)
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.ForceRequested);
        }

        if (!lastFrontSurface.HasValue || !lastPlan.HasValue)
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.MissingLastState);
        }

        if (lastFrontSurface.Value != currentFrontSurface)
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.FrontSurfaceChanged);
        }

        var lastTopmost = ToTopmostPlan(lastPlan.Value);
        var currentTopmost = ToTopmostPlan(currentPlan);
        if (lastTopmost != currentTopmost)
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.TopmostPlanChanged);
        }

        if (ShouldRetouchLauncherOnInteractiveSurface(currentFrontSurface, currentPlan))
        {
            return new FloatingTopmostApplyDecision(
                ShouldEnforceZOrder: true,
                Reason: FloatingTopmostApplyReason.LauncherInteractiveRetouch);
        }

        return new FloatingTopmostApplyDecision(
            ShouldEnforceZOrder: false,
            Reason: FloatingTopmostApplyReason.Unchanged);
    }

    internal static bool ShouldEnforceZOrder(
        ZOrderSurface? lastFrontSurface,
        ZOrderSurface currentFrontSurface,
        FloatingTopmostPlan? lastPlan,
        FloatingTopmostPlan currentPlan,
        bool forceEnforceZOrder = false)
    {
        return Resolve(
            lastFrontSurface,
            currentFrontSurface,
            lastPlan,
            currentPlan,
            forceEnforceZOrder).ShouldEnforceZOrder;
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
}

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

internal enum FloatingTopmostDriftReason
{
    None = 0,
    ToolbarDrift = 1,
    RollCallDrift = 2,
    LauncherDrift = 3,
    NoDrift = 4
}

internal readonly record struct FloatingTopmostDriftDecision(
    bool HasDrift,
    FloatingTopmostDriftReason Reason);

internal enum FloatingTopmostForceEnforceReason
{
    None = 0,
    DisabledByDesign = 1
}

internal readonly record struct FloatingTopmostForceEnforceDecision(
    bool ShouldForceEnforce,
    FloatingTopmostForceEnforceReason Reason);

internal static class FloatingTopmostDriftPolicy
{
    internal static FloatingTopmostDriftDecision ResolveDrift(ToolbarInteractionRetouchSnapshot snapshot)
    {
        if (snapshot.ToolbarVisible && !snapshot.ToolbarTopmost)
        {
            return new FloatingTopmostDriftDecision(
                HasDrift: true,
                Reason: FloatingTopmostDriftReason.ToolbarDrift);
        }

        if (snapshot.RollCallVisible && !snapshot.RollCallTopmost)
        {
            return new FloatingTopmostDriftDecision(
                HasDrift: true,
                Reason: FloatingTopmostDriftReason.RollCallDrift);
        }

        if (snapshot.LauncherVisible && !snapshot.LauncherTopmost)
        {
            return new FloatingTopmostDriftDecision(
                HasDrift: true,
                Reason: FloatingTopmostDriftReason.LauncherDrift);
        }

        return new FloatingTopmostDriftDecision(
            HasDrift: false,
            Reason: FloatingTopmostDriftReason.NoDrift);
    }

    internal static bool HasDrift(ToolbarInteractionRetouchSnapshot snapshot)
    {
        return ResolveDrift(snapshot).HasDrift;
    }

    internal static FloatingTopmostForceEnforceDecision ResolveForceEnforce(ToolbarInteractionRetouchSnapshot snapshot)
    {
        _ = snapshot;
        return new FloatingTopmostForceEnforceDecision(
            ShouldForceEnforce: false,
            Reason: FloatingTopmostForceEnforceReason.DisabledByDesign);
    }

    internal static bool ShouldForceEnforce(ToolbarInteractionRetouchSnapshot snapshot)
    {
        return ResolveForceEnforce(snapshot).ShouldForceEnforce;
    }
}

internal static class FloatingTopmostDriftRepairEnforcePolicy
{
    internal static bool Resolve(ToolbarInteractionRetouchSnapshot snapshot, ToolbarInteractionRetouchTrigger trigger)
    {
        if (trigger != ToolbarInteractionRetouchTrigger.Activated)
        {
            return false;
        }

        if (!InteractiveSceneIntervalPolicy.IsInteractiveScene(
                snapshot.OverlayVisible,
                snapshot.PhotoModeActive,
                snapshot.WhiteboardActive))
        {
            return false;
        }

        return snapshot.LauncherVisible && !snapshot.LauncherTopmost;
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

internal static class FloatingTopmostDriftRepairPolicy
{
    internal static FloatingTopmostDriftRepairPlan Resolve(ToolbarInteractionRetouchSnapshot snapshot)
    {
        return new FloatingTopmostDriftRepairPlan(
            RepairToolbar: snapshot.ToolbarVisible && !snapshot.ToolbarTopmost,
            RepairRollCall: snapshot.RollCallVisible && !snapshot.RollCallTopmost,
            RepairLauncher: snapshot.LauncherVisible && !snapshot.LauncherTopmost);
    }
}

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

internal static class FloatingTopmostPlanPolicy
{
    internal static FloatingTopmostPlan Resolve(
        ZOrderSurface frontSurface,
        FloatingTopmostVisibilitySnapshot snapshot)
    {
        return Resolve(
            frontSurface,
            snapshot.ToolbarVisible,
            snapshot.RollCallVisible,
            snapshot.LauncherVisible,
            snapshot.ImageManagerVisible,
            snapshot.OverlayVisible);
    }

    internal static FloatingTopmostPlan Resolve(
        ZOrderSurface frontSurface,
        bool toolbarVisible,
        bool rollCallVisible,
        bool launcherVisible,
        bool imageManagerVisible,
        bool overlayVisible)
    {
        var imageManagerTopmostDecision = ImageManagerTopmostPolicy.Resolve(imageManagerVisible, frontSurface);
        var overlayActivationSurfaceDecision = OverlayActivationSurfacePolicy.Resolve(overlayVisible, frontSurface);
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
}

internal enum FloatingTopmostRetouchReason
{
    None = 0,
    OverlayTopmostNotRising = 1,
    OverlayTopmostBecameRequired = 2
}

internal readonly record struct FloatingTopmostRetouchDecision(
    bool ShouldEnsureFloatingOnTransition,
    FloatingTopmostRetouchReason Reason);

internal static class FloatingTopmostRetouchPolicy
{
    internal static FloatingTopmostRetouchDecision Resolve(UiSessionTransition transition)
    {
        var shouldEnsure = transition.Previous.OverlayTopmostRequired != transition.Current.OverlayTopmostRequired
            && transition.Current.OverlayTopmostRequired;
        return shouldEnsure
            ? new FloatingTopmostRetouchDecision(
                ShouldEnsureFloatingOnTransition: true,
                Reason: FloatingTopmostRetouchReason.OverlayTopmostBecameRequired)
            : new FloatingTopmostRetouchDecision(
                ShouldEnsureFloatingOnTransition: false,
                Reason: FloatingTopmostRetouchReason.OverlayTopmostNotRising);
    }

    internal static bool ShouldEnsureFloatingOnTransition(UiSessionTransition transition)
    {
        return Resolve(transition).ShouldEnsureFloatingOnTransition;
    }
}

internal static class FloatingTopmostWatchdogPolicy
{
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
