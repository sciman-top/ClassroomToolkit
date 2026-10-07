using
System.Collections.Generic;
using
System.Windows;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingWindowCoordinationState(
    ZOrderSurface? LastFrontSurface,
    FloatingTopmostPlan? LastTopmostPlan)
{
    internal static FloatingWindowCoordinationState Default => new(
        LastFrontSurface: null,
        LastTopmostPlan: null);
}

internal readonly record struct FloatingWindowExecutionPlan(
    FloatingTopmostExecutionPlan TopmostExecutionPlan,
    FloatingWindowActivationPlan ActivationPlan,
    FloatingOwnerExecutionPlan OwnerPlan,
    bool ReplayOverlayBelowFloatingUtilities = false);

internal enum FloatingWindowExecutionSkipReason
{
    EnforceZOrder,
    ActivationIntent,
    OwnerBindingIntent,
    NoExecutionIntent
}

internal static class FloatingWindowCoordinator
{
    internal static FloatingWindowCoordinationState Apply(
        IWindowOrchestrator windowOrchestrator,
        IList<ZOrderSurface> surfaceStack,
        FloatingWindowCoordinationSnapshot coordination,
        FloatingWindowCoordinationState state,
        bool forceEnforceZOrder,
        bool suppressOverlayActivation,
        Action<FloatingWindowExecutionPlan> executePlan)
    {
        ArgumentNullException.ThrowIfNull(windowOrchestrator);
        ArgumentNullException.ThrowIfNull(surfaceStack);
        ArgumentNullException.ThrowIfNull(executePlan);

        var frontSurface = FloatingFrontSurfaceResolver.Resolve(
            windowOrchestrator,
            surfaceStack,
            coordination.Runtime);
        var topmostPlan = FloatingTopmostPolicies.ResolvePlan(
            frontSurface,
            coordination.TopmostVisibility);
        var enforceZOrder = FloatingTopmostPolicies.ShouldEnforceZOrder(
            state.LastFrontSurface,
            frontSurface,
            state.LastTopmostPlan,
            topmostPlan,
            forceEnforceZOrder);
        var executionPlan = CreateExecutionPlan(
            coordination.Runtime,
            topmostPlan,
            enforceZOrder,
            coordination.UtilityActivity,
            coordination.Owner,
            suppressOverlayActivation);

        var executionReason = ResolveExecutionReason(executionPlan);
        if (executionReason != FloatingWindowExecutionSkipReason.NoExecutionIntent)
        {
            executePlan(executionPlan);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                $"[FloatingWindow][Execution] skip reason={executionReason}");
        }

        return new FloatingWindowCoordinationState(
            LastFrontSurface: frontSurface,
            LastTopmostPlan: topmostPlan);
    }

    internal static FloatingWindowCoordinationState Apply(
        IWindowOrchestrator windowOrchestrator,
        IList<ZOrderSurface> surfaceStack,
        FloatingWindowCoordinationSnapshot coordination,
        FloatingWindowCoordinationState state,
        bool forceEnforceZOrder,
        bool suppressOverlayActivation,
        Window? overlayWindow,
        Window? toolbarWindow,
        Window? rollCallWindow,
        Window? launcherWindow,
        Window? imageManagerWindow)
    {
        return Apply(
            windowOrchestrator,
            surfaceStack,
            coordination,
            state,
            forceEnforceZOrder,
            suppressOverlayActivation,
            plan => FloatingWindowExecutionExecutor.Apply(
                plan,
                overlayWindow,
                toolbarWindow,
                rollCallWindow,
                launcherWindow,
                imageManagerWindow));
    }

    private static FloatingWindowExecutionPlan CreateExecutionPlan(
        FloatingWindowRuntimeSnapshot runtimeSnapshot,
        FloatingTopmostPlan topmostPlan,
        bool enforceZOrder,
        FloatingUtilityActivitySnapshot utilityActivity,
        FloatingOwnerRuntimeSnapshot ownerSnapshot,
        bool suppressOverlayActivation)
    {
        var activationPlan = FloatingWindowCoordinationPolicies.ResolveFloatingWindowActivation(
            runtimeSnapshot,
            topmostPlan,
            utilityActivity);

        return new FloatingWindowExecutionPlan(
            TopmostExecutionPlan: new FloatingTopmostExecutionPlan(
                ToolbarTopmost: topmostPlan.ToolbarTopmost,
                RollCallTopmost: topmostPlan.RollCallTopmost,
                LauncherTopmost: topmostPlan.LauncherTopmost,
                ImageManagerTopmost: topmostPlan.ImageManagerTopmost,
                EnforceZOrder: enforceZOrder),
            ActivationPlan: OverlayActivationSuppressionPolicyAdapter.ApplySuppression(
                activationPlan,
                suppressOverlayActivation),
            OwnerPlan: FloatingOwnerExecutionPlanPolicy.Resolve(ownerSnapshot),
            ReplayOverlayBelowFloatingUtilities: ShouldReplayOverlayBelowFloatingUtilities(
                runtimeSnapshot,
                topmostPlan,
                enforceZOrder));
    }

    private static FloatingWindowExecutionSkipReason ResolveExecutionReason(FloatingWindowExecutionPlan plan)
    {
        if (plan.TopmostExecutionPlan.EnforceZOrder)
        {
            return FloatingWindowExecutionSkipReason.EnforceZOrder;
        }

        if (plan.ActivationPlan.ActivateOverlay || plan.ActivationPlan.ActivateImageManager)
        {
            return FloatingWindowExecutionSkipReason.ActivationIntent;
        }

        if (plan.OwnerPlan.ToolbarAction != FloatingOwnerBindingAction.None
            || plan.OwnerPlan.RollCallAction != FloatingOwnerBindingAction.None
            || plan.OwnerPlan.ImageManagerAction != FloatingOwnerBindingAction.None)
        {
            return FloatingWindowExecutionSkipReason.OwnerBindingIntent;
        }

        return FloatingWindowExecutionSkipReason.NoExecutionIntent;
    }

    private static bool ShouldReplayOverlayBelowFloatingUtilities(
        FloatingWindowRuntimeSnapshot runtimeSnapshot,
        FloatingTopmostPlan topmostPlan,
        bool enforceZOrder)
    {
        if ((!runtimeSnapshot.PhotoActive && !runtimeSnapshot.PresentationFullscreen)
            || !runtimeSnapshot.OverlayVisible
            || !enforceZOrder)
        {
            return false;
        }

        return topmostPlan.ToolbarTopmost
            || topmostPlan.RollCallTopmost
            || topmostPlan.LauncherTopmost
            || topmostPlan.ImageManagerTopmost;
    }
}

internal readonly record struct FloatingWindowCoordinationSnapshot(
    FloatingWindowRuntimeSnapshot Runtime,
    LauncherWindowRuntimeSnapshot Launcher,
    FloatingTopmostVisibilitySnapshot TopmostVisibility,
    FloatingUtilityActivitySnapshot UtilityActivity,
    FloatingOwnerRuntimeSnapshot Owner);

internal readonly record struct FloatingWindowRuntimeSnapshot(
    bool OverlayVisible,
    bool OverlayActive,
    bool PhotoActive,
    bool PresentationFullscreen,
    bool WhiteboardActive,
    bool ImageManagerVisible,
    bool LauncherVisible);

internal readonly record struct FloatingTopmostVisibilitySnapshot(
    bool ToolbarVisible,
    bool RollCallVisible,
    bool LauncherVisible,
    bool ImageManagerVisible,
    bool OverlayVisible);

internal readonly record struct FloatingUtilityActivitySnapshot(
    bool ToolbarActive,
    bool RollCallActive,
    bool ImageManagerActive,
    bool LauncherActive);

internal readonly record struct FloatingOwnerRuntimeSnapshot(
    bool OverlayVisible,
    bool ToolbarOwnerAlreadyOverlay,
    bool RollCallOwnerAlreadyOverlay,
    bool ImageManagerOwnerAlreadyOverlay);

internal readonly record struct FloatingWindowActivationSnapshot(
    bool OverlayVisible,
    bool OverlayShouldActivate,
    bool OverlayActive,
    bool ImageManagerTopmost,
    bool ImageManagerActive,
    FloatingUtilityActivitySnapshot UtilityActivity);

internal readonly record struct FloatingWindowActivationPlan(
    bool ActivateOverlay,
    bool ActivateImageManager);

internal static class FloatingWindowExecutionExecutor
{
    internal static void Apply(
        FloatingWindowExecutionPlan plan,
        Window? overlayWindow,
        Window? toolbarWindow,
        Window? rollCallWindow,
        Window? launcherWindow,
        Window? imageManagerWindow)
    {
        Apply(
            plan,
            overlayWindow,
            toolbarWindow,
            rollCallWindow,
            launcherWindow,
            imageManagerWindow,
            (ownerPlan, overlay, toolbar, rollCall, imageManager) =>
                FloatingOwnerExecutionExecutor.Apply(ownerPlan, overlay, toolbar, rollCall, imageManager),
            (target, shouldActivate) => WindowActivationExecutor.TryActivate(target, shouldActivate),
            (topmostPlan, toolbar, rollCall, launcher, imageManager) =>
                FloatingTopmostExecutionExecutor.Apply(topmostPlan, toolbar, rollCall, launcher, imageManager),
            WindowTopmostExecutor.ApplyNoActivate);
    }

    internal static void Apply<TWindow>(
        FloatingWindowExecutionPlan plan,
        TWindow? overlayWindow,
        TWindow? toolbarWindow,
        TWindow? rollCallWindow,
        TWindow? launcherWindow,
        TWindow? imageManagerWindow,
        Action<FloatingOwnerExecutionPlan, TWindow?, TWindow?, TWindow?, TWindow?> applyOwnerPlan,
        Func<TWindow?, bool, bool> tryActivate,
        Action<FloatingTopmostExecutionPlan, TWindow?, TWindow?, TWindow?, TWindow?> applyTopmostPlan,
        Action<TWindow?, bool, bool>? applyOverlayTopmostNoActivate = null)
        where TWindow : class
    {
        ArgumentNullException.ThrowIfNull(applyOwnerPlan);
        ArgumentNullException.ThrowIfNull(tryActivate);
        ArgumentNullException.ThrowIfNull(applyTopmostPlan);

        SafeActionExecutionExecutor.TryExecute(
            () => applyOwnerPlan(plan.OwnerPlan, overlayWindow, toolbarWindow, rollCallWindow, imageManagerWindow));

        var imageManagerActivationDecision = FloatingWindowCoordinationPolicies.Resolve(
            imageManagerWindow,
            plan.ActivationPlan.ActivateImageManager);
        ExecuteActivation(
            imageManagerWindow,
            imageManagerActivationDecision,
            "ImageManager",
            tryActivate);

        var overlayActivationDecision = FloatingWindowCoordinationPolicies.Resolve(
            overlayWindow,
            plan.ActivationPlan.ActivateOverlay);
        ExecuteActivation(
            overlayWindow,
            overlayActivationDecision,
            "Overlay",
            tryActivate);

        if (plan.ReplayOverlayBelowFloatingUtilities && applyOverlayTopmostNoActivate != null)
        {
            // WPF Topmost 属性去重后感知不到 topmost 带内漂移（放映窗创建/前台切换会把
            // 覆盖层压到下面），必须把 enforce 透传给原生 SetWindowPos 重断言。
            SafeActionExecutionExecutor.TryExecute(
                () => applyOverlayTopmostNoActivate(overlayWindow, true, plan.TopmostExecutionPlan.EnforceZOrder));
        }

        SafeActionExecutionExecutor.TryExecute(
            () => applyTopmostPlan(
                plan.TopmostExecutionPlan,
                toolbarWindow,
                rollCallWindow,
                launcherWindow,
                imageManagerWindow));
    }

    private static void ExecuteActivation<TWindow>(
        TWindow? target,
        FloatingActivationExecutionDecision decision,
        string targetName,
        Func<TWindow?, bool, bool> tryActivate)
        where TWindow : class
    {
        if (decision.ShouldActivate)
        {
            var activated = SafeActionExecutionExecutor.TryExecute(
                () => tryActivate(target, true),
                fallback: false);

            if (!activated)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[FloatingWindow][Activation] attempt-failed target={targetName}");
            }

            return;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[FloatingWindow][Activation] skip target={targetName} reason={decision.Reason}");
    }
}

internal enum FloatingActivationExecutionReason
{
    None = 0,
    TargetMissing = 1,
    ActivationNotRequested = 2
}

internal readonly record struct FloatingActivationExecutionDecision(
    bool ShouldActivate,
    FloatingActivationExecutionReason Reason);

internal enum FloatingActivationGuardReason
{
    None = 0,
    ToolbarActive = 1,
    RollCallActive = 2,
    ImageManagerActive = 3,
    LauncherActive = 4
}

internal readonly record struct FloatingActivationGuardDecision(
    bool IsBlocked,
    FloatingActivationGuardReason Reason);

internal static class FloatingFrontSurfaceResolver
{
    internal static ZOrderSurface Resolve(
        IWindowOrchestrator windowOrchestrator,
        IList<ZOrderSurface> surfaceStack,
        FloatingWindowRuntimeSnapshot snapshot)
    {
        windowOrchestrator.PruneSurfaceStack(
            surfaceStack,
            snapshot.PhotoActive,
            snapshot.PresentationFullscreen,
            snapshot.WhiteboardActive,
            snapshot.ImageManagerVisible);

        return windowOrchestrator.ResolveFrontSurface(
            surfaceStack as IReadOnlyList<ZOrderSurface> ?? new List<ZOrderSurface>(surfaceStack),
            snapshot.PhotoActive,
            snapshot.PresentationFullscreen,
            snapshot.WhiteboardActive,
            snapshot.ImageManagerVisible);
    }
}

internal enum FloatingDispatchQueueAction
{
    None = 0,
    QueueApply = 1
}

internal enum FloatingDispatchQueueReason
{
    None = 0,
    QueuedNewRequest = 1,
    MergedIntoQueuedRequest = 2,
    QueueDispatchFailed = 3
}

internal readonly record struct FloatingDispatchQueueState(
    bool ApplyQueued,
    bool ForceEnforceZOrder)
{
    public static FloatingDispatchQueueState Default => new(
        ApplyQueued: false,
        ForceEnforceZOrder: false);
}

internal readonly record struct FloatingDispatchQueueDecision(
    FloatingDispatchQueueState State,
    FloatingDispatchQueueAction Action,
    FloatingDispatchQueueReason Reason);

internal static class FloatingWindowCoordinationPolicies
{
    public static FloatingWindowRuntimeSnapshot ResolveFloatingWindowRuntimeSnapshot(
        bool overlayVisible,
        bool overlayActive,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible,
        bool imageManagerMinimized,
        bool launcherVisible)
    {
        return new FloatingWindowRuntimeSnapshot(
            OverlayVisible: overlayVisible,
            OverlayActive: overlayActive,
            PhotoActive: photoActive,
            PresentationFullscreen: presentationFullscreen,
            WhiteboardActive: whiteboardActive,
            ImageManagerVisible: imageManagerVisible && !imageManagerMinimized,
            LauncherVisible: launcherVisible);
    }

    internal static FloatingWindowActivationPlan ResolveFloatingWindowActivation(
        FloatingWindowRuntimeSnapshot runtimeSnapshot,
        FloatingTopmostPlan topmostPlan,
        FloatingUtilityActivitySnapshot utilityActivity)
    {
        return ResolveFloatingWindowActivation(new FloatingWindowActivationSnapshot(
            OverlayVisible: runtimeSnapshot.OverlayVisible,
            OverlayShouldActivate: topmostPlan.OverlayShouldActivate,
            OverlayActive: runtimeSnapshot.OverlayActive,
            ImageManagerTopmost: topmostPlan.ImageManagerTopmost,
            ImageManagerActive: utilityActivity.ImageManagerActive,
            UtilityActivity: utilityActivity));
    }

    internal static FloatingWindowActivationPlan ResolveFloatingWindowActivation(FloatingWindowActivationSnapshot snapshot)
    {
        var overlayDecision = OverlayActivationPolicies.ResolveOverlayActivation(
            overlayVisible: snapshot.OverlayVisible,
            overlayShouldActivate: snapshot.OverlayShouldActivate,
            overlayActive: snapshot.OverlayActive,
            toolbarActive: snapshot.UtilityActivity.ToolbarActive,
            imageManagerActive: snapshot.UtilityActivity.ImageManagerActive,
            rollCallActive: snapshot.UtilityActivity.RollCallActive,
            launcherActive: snapshot.UtilityActivity.LauncherActive);
        var imageManagerDecision = ImageManagerWindowingPolicies.ResolveImageManagerActivation(
            imageManagerTopmost: snapshot.ImageManagerTopmost,
            imageManagerActive: snapshot.ImageManagerActive,
            toolbarActive: snapshot.UtilityActivity.ToolbarActive,
            rollCallActive: snapshot.UtilityActivity.RollCallActive,
            launcherActive: snapshot.UtilityActivity.LauncherActive);

        return new FloatingWindowActivationPlan(
            ActivateOverlay: overlayDecision.ShouldActivate,
            ActivateImageManager: imageManagerDecision.ShouldActivate);
    }

    internal static FloatingActivationExecutionDecision Resolve<TWindow>(TWindow? target, bool shouldActivate)
        where TWindow : class
    {
        if (target == null)
        {
            return new FloatingActivationExecutionDecision(
                ShouldActivate: false,
                Reason: FloatingActivationExecutionReason.TargetMissing);
        }

        return shouldActivate
            ? new FloatingActivationExecutionDecision(
                ShouldActivate: true,
                Reason: FloatingActivationExecutionReason.None)
            : new FloatingActivationExecutionDecision(
                ShouldActivate: false,
                Reason: FloatingActivationExecutionReason.ActivationNotRequested);
    }

    internal static bool ShouldActivate<TWindow>(TWindow? target, bool shouldActivate)
        where TWindow : class
    {
        return Resolve(target, shouldActivate).ShouldActivate;
    }

    internal static FloatingActivationGuardDecision ResolveFloatingActivationGuard(FloatingUtilityActivitySnapshot snapshot)
    {
        if (snapshot.ToolbarActive)
        {
            return new FloatingActivationGuardDecision(
                IsBlocked: true,
                Reason: FloatingActivationGuardReason.ToolbarActive);
        }

        if (snapshot.RollCallActive)
        {
            return new FloatingActivationGuardDecision(
                IsBlocked: true,
                Reason: FloatingActivationGuardReason.RollCallActive);
        }

        if (snapshot.ImageManagerActive)
        {
            return new FloatingActivationGuardDecision(
                IsBlocked: true,
                Reason: FloatingActivationGuardReason.ImageManagerActive);
        }

        if (snapshot.LauncherActive)
        {
            return new FloatingActivationGuardDecision(
                IsBlocked: true,
                Reason: FloatingActivationGuardReason.LauncherActive);
        }

        return new FloatingActivationGuardDecision(
            IsBlocked: false,
            Reason: FloatingActivationGuardReason.None);
    }

    internal static bool IsBlockedByUtilityWindows(FloatingUtilityActivitySnapshot snapshot)
    {
        return ResolveFloatingActivationGuard(snapshot).IsBlocked;
    }

    internal static bool IsBlockedByUtilityWindows(
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive)
    {
        return IsBlockedByUtilityWindows(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));
    }

    internal static FloatingDispatchQueueDecision RequestApply(
        FloatingDispatchQueueState state,
        bool forceEnforceZOrder = false)
    {
        if (state.ApplyQueued)
        {
            var merged = state with
            {
                ForceEnforceZOrder = state.ForceEnforceZOrder || forceEnforceZOrder
            };
            return new FloatingDispatchQueueDecision(
                merged,
                FloatingDispatchQueueAction.None,
                FloatingDispatchQueueReason.MergedIntoQueuedRequest);
        }

        var next = state with
        {
            ApplyQueued = true,
            ForceEnforceZOrder = forceEnforceZOrder
        };
        return new FloatingDispatchQueueDecision(
            next,
            FloatingDispatchQueueAction.QueueApply,
            FloatingDispatchQueueReason.QueuedNewRequest);
    }

    internal static FloatingDispatchQueueState OnApplyExecuted(FloatingDispatchQueueState state)
        => state with
        {
            ApplyQueued = false,
            ForceEnforceZOrder = false
        };
}
