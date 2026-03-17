using System;
using System.Windows;

namespace ClassroomToolkit.App.Windowing;

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
