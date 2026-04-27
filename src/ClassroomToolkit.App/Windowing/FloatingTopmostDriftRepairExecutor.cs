using System;
using System.Windows;

namespace ClassroomToolkit.App.Windowing;

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
