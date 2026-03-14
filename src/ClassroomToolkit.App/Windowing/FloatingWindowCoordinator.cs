using System;
using System.Collections.Generic;
using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingWindowCoordinationState(
    ZOrderSurface? LastFrontSurface,
    FloatingTopmostPlan? LastTopmostPlan);

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

        var decision = FloatingWindowZOrderDecisionPolicy.Resolve(
            windowOrchestrator,
            surfaceStack,
            coordination.Runtime,
            coordination.TopmostVisibility,
            state.LastFrontSurface,
            state.LastTopmostPlan,
            forceEnforceZOrder);
        var executionPlan = FloatingWindowExecutionPlanPolicy.Resolve(
            coordination.Runtime,
            decision.TopmostPlan,
            decision.EnforceZOrder,
            coordination.UtilityActivity,
            coordination.Owner,
            suppressOverlayActivation);

        var skipDecision = FloatingWindowExecutionSkipPolicy.Resolve(executionPlan);
        if (skipDecision.ShouldExecute)
        {
            executePlan(executionPlan);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                FloatingWindowDiagnosticsPolicy.FormatExecutionSkipMessage(
                    skipDecision.Reason));
        }

        return new FloatingWindowCoordinationState(
            LastFrontSurface: decision.FrontSurface,
            LastTopmostPlan: decision.TopmostPlan);
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
}
