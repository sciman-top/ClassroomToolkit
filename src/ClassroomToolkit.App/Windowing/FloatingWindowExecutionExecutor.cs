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
