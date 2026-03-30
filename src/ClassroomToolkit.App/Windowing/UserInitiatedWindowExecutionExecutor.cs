using System.Windows;
using System;

namespace ClassroomToolkit.App.Windowing;

internal static class UserInitiatedWindowExecutionExecutor
{
    internal static bool Apply(Window? window, UserInitiatedWindowActivationDecision decision)
    {
        return Apply(window, decision.ShouldActivateAfterShow);
    }

    internal static bool Apply(Window? window, bool shouldActivate)
    {
        return Apply(
            window,
            shouldActivate,
            (target, activate) => WindowActivationExecutor.TryActivate(target, activate));
    }

    internal static bool Apply<TWindow>(
        TWindow? window,
        UserInitiatedWindowActivationDecision decision,
        Func<TWindow?, bool, bool> tryActivate)
        where TWindow : class
    {
        return Apply(window, decision.ShouldActivateAfterShow, tryActivate);
    }

    internal static bool Apply<TWindow>(
        TWindow? window,
        bool shouldActivate,
        Func<TWindow?, bool, bool> tryActivate)
        where TWindow : class
    {
        ArgumentNullException.ThrowIfNull(tryActivate);

        return SafeActionExecutionExecutor.TryExecute(
            () => tryActivate(window, shouldActivate),
            fallback: false);
    }
}
