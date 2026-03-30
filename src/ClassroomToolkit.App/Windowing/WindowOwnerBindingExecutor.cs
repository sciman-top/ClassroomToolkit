using System.Windows;
using System;

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
        ArgumentNullException.ThrowIfNull(attachAction);
        ArgumentNullException.ThrowIfNull(detachAction);

        if (child == null)
        {
            return false;
        }

        switch (action)
        {
            case FloatingOwnerBindingAction.AttachOverlay when overlayOwner != null:
                return SafeActionExecutionExecutor.TryExecute(
                    () =>
                    {
                        attachAction(child, overlayOwner);
                        return true;
                    },
                    fallback: false);
            case FloatingOwnerBindingAction.DetachOverlay:
                return SafeActionExecutionExecutor.TryExecute(
                    () =>
                    {
                        detachAction(child);
                        return true;
                    },
                    fallback: false);
            default:
                return false;
        }
    }
}
