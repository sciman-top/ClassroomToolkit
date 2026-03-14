using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowOwnerBindingExecutor
{
    internal static bool TryApply(Window? child, Window? overlayOwner, FloatingOwnerBindingAction action)
    {
        return TryExecute(
            child,
            overlayOwner,
            action,
            attachAction: (target, owner) => target.Owner = owner,
            detachAction: target => target.Owner = null);
    }

    internal static bool TryExecute<TTarget, TOwner>(
        TTarget? child,
        TOwner? overlayOwner,
        FloatingOwnerBindingAction action,
        Action<TTarget, TOwner> attachAction,
        Action<TTarget> detachAction)
        where TTarget : class
        where TOwner : class
    {
        if (child == null)
        {
            return false;
        }

        switch (action)
        {
            case FloatingOwnerBindingAction.AttachOverlay when overlayOwner != null:
                attachAction(child, overlayOwner);
                return true;
            case FloatingOwnerBindingAction.DetachOverlay:
                detachAction(child);
                return true;
            default:
                return false;
        }
    }
}
