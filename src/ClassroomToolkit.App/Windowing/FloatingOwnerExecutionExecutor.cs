using System.Windows;

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
        applyAction(toolbarWindow, overlayOwner, plan.ToolbarAction);
        applyAction(rollCallWindow, overlayOwner, plan.RollCallAction);
        applyAction(imageManagerWindow, overlayOwner, plan.ImageManagerAction);
    }
}
