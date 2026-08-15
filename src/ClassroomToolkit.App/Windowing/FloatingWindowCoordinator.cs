using System;
using System.Collections.Generic;
using System.Windows;

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
        var topmostPlan = FloatingTopmostPlanPolicy.Resolve(
            frontSurface,
            coordination.TopmostVisibility);
        var enforceZOrder = FloatingTopmostApplyPolicy.ShouldEnforceZOrder(
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
        var activationPlan = FloatingWindowActivationPolicy.Resolve(
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
        if (!runtimeSnapshot.PhotoActive || !runtimeSnapshot.OverlayVisible || !enforceZOrder)
        {
            return false;
        }

        return topmostPlan.ToolbarTopmost
            || topmostPlan.RollCallTopmost
            || topmostPlan.LauncherTopmost
            || topmostPlan.ImageManagerTopmost;
    }
}
