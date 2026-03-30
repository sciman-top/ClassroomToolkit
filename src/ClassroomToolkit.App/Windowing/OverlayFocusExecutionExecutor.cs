using System.Windows;
using System.Windows.Input;
using System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct OverlayFocusExecutionDecision(
    bool ShouldActivate,
    bool ShouldKeyboardFocus);

internal static class OverlayFocusExecutionExecutor
{
    internal static void Apply(
        Window? target,
        bool shouldActivate,
        bool shouldKeyboardFocus)
    {
        Apply(
            target,
            shouldActivate,
            shouldKeyboardFocus,
            (window, activate) => WindowActivationExecutor.TryActivate(window, activate),
            (element, focus) => WindowActivationExecutor.TryKeyboardFocus(element, focus));
    }

    internal static void Apply<TTarget>(
        TTarget? target,
        bool shouldActivate,
        bool shouldKeyboardFocus,
        Func<TTarget?, bool, bool> tryActivate,
        Func<TTarget?, bool, bool> tryKeyboardFocus)
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(tryActivate);
        ArgumentNullException.ThrowIfNull(tryKeyboardFocus);

        var decision = Resolve(shouldActivate, shouldKeyboardFocus);
        _ = SafeActionExecutionExecutor.TryExecute(() => tryActivate(target, decision.ShouldActivate));
        _ = SafeActionExecutionExecutor.TryExecute(() => tryKeyboardFocus(target, decision.ShouldKeyboardFocus));
    }

    internal static OverlayFocusExecutionDecision Resolve(
        bool shouldActivate,
        bool shouldKeyboardFocus)
    {
        return new OverlayFocusExecutionDecision(
            ShouldActivate: shouldActivate,
            ShouldKeyboardFocus: shouldKeyboardFocus);
    }
}
