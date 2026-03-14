using System.Windows;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowStateTransitionExecutor
{
    internal static bool Apply(Window? target, WindowState targetState)
    {
        return Apply(
            target,
            targetState,
            (window, requestedState) =>
            {
                if (window == null)
                {
                    return false;
                }

                window.WindowState = requestedState;
                return true;
            });
    }

    internal static bool Apply<TTarget>(
        TTarget? target,
        WindowState targetState,
        Func<TTarget?, WindowState, bool> applyState)
        where TTarget : class
    {
        if (target == null)
        {
            return false;
        }

        return applyState(target, targetState);
    }
}
