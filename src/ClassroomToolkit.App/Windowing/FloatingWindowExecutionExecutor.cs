using System;
using System.Windows;

namespace ClassroomToolkit.App.Windowing;

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
                FloatingTopmostExecutionExecutor.Apply(topmostPlan, toolbar, rollCall, launcher, imageManager));
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
        Action<FloatingTopmostExecutionPlan, TWindow?, TWindow?, TWindow?, TWindow?> applyTopmostPlan)
        where TWindow : class
    {
        ArgumentNullException.ThrowIfNull(applyOwnerPlan);
        ArgumentNullException.ThrowIfNull(tryActivate);
        ArgumentNullException.ThrowIfNull(applyTopmostPlan);

        SafeActionExecutionExecutor.TryExecute(
            () => applyOwnerPlan(plan.OwnerPlan, overlayWindow, toolbarWindow, rollCallWindow, imageManagerWindow));

        var imageManagerActivationDecision = FloatingActivationExecutionPolicy.Resolve(
            imageManagerWindow,
            plan.ActivationPlan.ActivateImageManager);
        ExecuteActivation(
            imageManagerWindow,
            imageManagerActivationDecision,
            "ImageManager",
            tryActivate);

        var overlayActivationDecision = FloatingActivationExecutionPolicy.Resolve(
            overlayWindow,
            plan.ActivationPlan.ActivateOverlay);
        ExecuteActivation(
            overlayWindow,
            overlayActivationDecision,
            "Overlay",
            tryActivate);

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
                    FloatingWindowDiagnosticsPolicy.FormatActivationAttemptFailedMessage(targetName));
            }

            return;
        }

        System.Diagnostics.Debug.WriteLine(
            FloatingWindowDiagnosticsPolicy.FormatActivationSkipMessage(
                targetName,
                decision.Reason));
    }
}
