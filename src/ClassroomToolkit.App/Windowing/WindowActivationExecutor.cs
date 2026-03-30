using System;
using System.Windows;
using System.Windows.Input;

namespace ClassroomToolkit.App.Windowing;

internal enum WindowActivationExecutionReason
{
    None = 0,
    ExecutionNotRequested = 1,
    TargetMissing = 2,
    Executed = 3
}

internal readonly record struct WindowActivationExecutionDecision(
    bool ShouldExecute,
    WindowActivationExecutionReason Reason);

internal static class WindowActivationExecutor
{
    internal static bool TryActivate(Window? window, bool shouldActivate)
    {
        return TryExecute(window, shouldActivate, target => target.Activate());
    }

    internal static bool TryKeyboardFocus(IInputElement? element, bool shouldFocus)
    {
        return TryExecute(element, shouldFocus, target => Keyboard.Focus(target));
    }

    internal static bool TryExecute<TTarget>(
        TTarget? target,
        bool shouldExecute,
        Action<TTarget> executeAction)
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(executeAction);

        var decision = ResolveExecution(target, shouldExecute);
        if (!decision.ShouldExecute)
        {
            return false;
        }

        executeAction(target!);
        return true;
    }

    internal static WindowActivationExecutionDecision ResolveExecution<TTarget>(
        TTarget? target,
        bool shouldExecute)
        where TTarget : class
    {
        if (!shouldExecute)
        {
            return new WindowActivationExecutionDecision(
                ShouldExecute: false,
                Reason: WindowActivationExecutionReason.ExecutionNotRequested);
        }

        if (target == null)
        {
            return new WindowActivationExecutionDecision(
                ShouldExecute: false,
                Reason: WindowActivationExecutionReason.TargetMissing);
        }

        return new WindowActivationExecutionDecision(
            ShouldExecute: true,
            Reason: WindowActivationExecutionReason.Executed);
    }
}
