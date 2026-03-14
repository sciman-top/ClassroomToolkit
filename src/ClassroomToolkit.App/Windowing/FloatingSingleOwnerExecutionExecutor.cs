using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal static class FloatingSingleOwnerExecutionExecutor
{
    internal static bool Apply(
        FloatingOwnerBindingAction action,
        Window? child,
        Window? overlayOwner)
    {
        return Apply(
            action,
            child,
            overlayOwner,
            (target, owner, ownerAction) => WindowOwnerBindingExecutor.TryApply(target, owner, ownerAction));
    }

    internal static bool Apply<TWindow>(
        FloatingOwnerBindingAction action,
        TWindow? child,
        TWindow? overlayOwner,
        Func<TWindow?, TWindow?, FloatingOwnerBindingAction, bool> applyAction)
        where TWindow : class
    {
        return applyAction(child, overlayOwner, action);
    }
}
