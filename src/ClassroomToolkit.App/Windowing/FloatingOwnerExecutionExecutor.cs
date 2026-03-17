using System.Windows;
using System;

namespace ClassroomToolkit.App.Windowing;

internal static class FloatingOwnerExecutionExecutor
{
    internal static void Apply(
        FloatingOwnerExecutionPlan plan,
        Window? overlayOwner,
        Window? toolbarWindow,
        Window? rollCallWindow,
        Window? imageManagerWindow)
    {
        Apply(
            plan,
            overlayOwner,
            toolbarWindow,
            rollCallWindow,
            imageManagerWindow,
            (target, owner, action) => WindowOwnerBindingExecutor.TryApply(target, owner, action));
    }

    internal static void Apply<TTarget, TOwner>(
        FloatingOwnerExecutionPlan plan,
        TOwner? overlayOwner,
        TTarget? toolbarWindow,
        TTarget? rollCallWindow,
        TTarget? imageManagerWindow,
        Func<TTarget?, TOwner?, FloatingOwnerBindingAction, bool> applyAction)
        where TTarget : class
        where TOwner : class
    {
        ArgumentNullException.ThrowIfNull(applyAction);

        TryApply(toolbarWindow, overlayOwner, plan.ToolbarAction, applyAction);
        TryApply(rollCallWindow, overlayOwner, plan.RollCallAction, applyAction);
        TryApply(imageManagerWindow, overlayOwner, plan.ImageManagerAction, applyAction);
    }

    private static void TryApply<TTarget, TOwner>(
        TTarget? target,
        TOwner? overlayOwner,
        FloatingOwnerBindingAction action,
        Func<TTarget?, TOwner?, FloatingOwnerBindingAction, bool> applyAction)
        where TTarget : class
        where TOwner : class
    {
        _ = SafeActionExecutionExecutor.TryExecute(() => applyAction(target, overlayOwner, action));
    }
}
