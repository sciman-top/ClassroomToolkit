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
        if (applyTopmostNoActivate == null)
        {
            throw new ArgumentNullException(nameof(applyTopmostNoActivate));
        }

        applyTopmostNoActivate(toolbarWindow, plan.ToolbarTopmost, plan.EnforceZOrder);
        applyTopmostNoActivate(rollCallWindow, plan.RollCallTopmost, plan.EnforceZOrder);
        applyTopmostNoActivate(launcherWindow, plan.LauncherTopmost, plan.EnforceZOrder);
        applyTopmostNoActivate(imageManagerWindow, plan.ImageManagerTopmost, plan.EnforceZOrder);
    }
}
