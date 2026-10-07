using
System.Threading;
using
System.Windows.Input;
using
System.Windows;
using
System;

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

internal enum UserInitiatedWindowActivationReason
{
    None = 0,
    WindowNotVisible = 1,
    WindowAlreadyActive = 2,
    ActivationRequired = 3
}

internal readonly record struct UserInitiatedWindowActivationDecision(
    bool ShouldActivateAfterShow,
    UserInitiatedWindowActivationReason Reason);

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
        ArgumentNullException.ThrowIfNull(applyState);

        if (target == null)
        {
            return false;
        }

        return SafeActionExecutionExecutor.TryExecute(
            () => applyState(target, targetState),
            fallback: false);
    }
}

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

internal static class SafeActionExecutionExecutor
{
    internal static bool TryExecute(Action action, Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            action();
            return true;
        }
        catch (Exception ex) when (WindowingDiagnosticsPolicies.IsNonFatal(ex))
        {
            if (onFailure != null)
            {
                try
                {
                    onFailure(ex);
                }
                catch (Exception callbackEx) when (WindowingDiagnosticsPolicies.IsNonFatal(callbackEx))
                {
                }
            }
            return false;
        }
    }

    internal static TResult TryExecute<TResult>(
        Func<TResult> action,
        TResult fallback = default!,
        Action<Exception>? onFailure = null)
    {
        ArgumentNullException.ThrowIfNull(action);

        try
        {
            return action();
        }
        catch (Exception ex) when (WindowingDiagnosticsPolicies.IsNonFatal(ex))
        {
            if (onFailure != null)
            {
                try
                {
                    onFailure(ex);
                }
                catch (Exception callbackEx) when (WindowingDiagnosticsPolicies.IsNonFatal(callbackEx))
                {
                }
            }

            return fallback;
        }
    }
}

internal enum WindowLifecycleSubscriptionReason
{
    None = 0,
    CurrentWindowMissing = 1,
    SameWindowInstance = 2,
    WindowInstanceChanged = 3
}

internal readonly record struct WindowLifecycleSubscriptionDecision(
    bool ShouldWire,
    WindowLifecycleSubscriptionReason Reason);

internal enum WindowCursorHitTestReason
{
    None = 0,
    InsideBounds = 1,
    OutsideBounds = 2
}

internal readonly record struct WindowCursorHitTestDecision(
    bool IsInside,
    WindowCursorHitTestReason Reason);

internal enum WindowCursorHitTestExecutionReason
{
    None = 0,
    InvalidWindowHandle = 1,
    CursorUnavailable = 2,
    WindowRectUnavailable = 3,
    HitTestCompleted = 4
}

internal readonly record struct WindowCursorHitTestExecutionDecision(
    bool Succeeded,
    bool IsInside,
    WindowCursorHitTestExecutionReason Reason);

internal static class WindowCursorHitTestExecutor
{
    private static ICursorWindowGeometryInteropAdapter _interopAdapter = new NativeCursorWindowGeometryInteropAdapter();

    internal static IDisposable PushInteropAdapterForTest(ICursorWindowGeometryInteropAdapter adapter)
    {
        var previous = _interopAdapter;
        _interopAdapter = adapter;
        return InteropAdapterScope.Create(() => _interopAdapter = previous);
    }

    internal static bool TryIsCursorInsideWindow(IntPtr hwnd, out bool inside)
    {
        var decision = Resolve(hwnd);
        inside = decision.IsInside;
        return decision.Succeeded;
    }

    internal static WindowCursorHitTestExecutionDecision Resolve(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return new WindowCursorHitTestExecutionDecision(
                Succeeded: false,
                IsInside: false,
                Reason: WindowCursorHitTestExecutionReason.InvalidWindowHandle);
        }

        if (!_interopAdapter.TryGetCursorPos(out var x, out var y))
        {
            return new WindowCursorHitTestExecutionDecision(
                Succeeded: false,
                IsInside: false,
                Reason: WindowCursorHitTestExecutionReason.CursorUnavailable);
        }

        if (!_interopAdapter.TryGetWindowRect(hwnd, out var left, out var top, out var right, out var bottom))
        {
            return new WindowCursorHitTestExecutionDecision(
                Succeeded: false,
                IsInside: false,
                Reason: WindowCursorHitTestExecutionReason.WindowRectUnavailable);
        }

        var hitTestDecision = WindowExecutionPolicies.ResolveWindowCursorHitTest(x, y, left, top, right, bottom);
        return new WindowCursorHitTestExecutionDecision(
            Succeeded: true,
            IsInside: hitTestDecision.IsInside,
            Reason: WindowCursorHitTestExecutionReason.HitTestCompleted);
    }
}

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

internal enum WindowStateNormalizationReason
{
    None = 0,
    TargetMissing = 1,
    NormalizationNotRequested = 2,
    NormalizationRequested = 3
}

internal readonly record struct WindowStateNormalizationDecision(
    bool ShouldNormalize,
    WindowStateNormalizationReason Reason);

internal static class WindowStateNormalizationExecutor
{
    internal static bool Apply(Window? target, bool shouldNormalize)
    {
        var decision = Resolve(target, shouldNormalize);
        return decision.ShouldNormalize && WindowStateTransitionExecutor.Apply(target, WindowState.Normal);
    }

    internal static bool Apply<TTarget>(
        TTarget? target,
        bool shouldNormalize,
        Func<TTarget?, bool, bool> applyNormalize)
        where TTarget : class
    {
        ArgumentNullException.ThrowIfNull(applyNormalize);

        var decision = Resolve(target, shouldNormalize);
        if (!decision.ShouldNormalize && target == null)
        {
            return false;
        }

        return SafeActionExecutionExecutor.TryExecute(
            () => applyNormalize(target, decision.ShouldNormalize),
            fallback: false);
    }

    internal static WindowStateNormalizationDecision Resolve<TTarget>(
        TTarget? target,
        bool shouldNormalize)
        where TTarget : class
    {
        if (target == null)
        {
            return new WindowStateNormalizationDecision(
                ShouldNormalize: false,
                Reason: WindowStateNormalizationReason.TargetMissing);
        }

        return shouldNormalize
            ? new WindowStateNormalizationDecision(
                ShouldNormalize: true,
                Reason: WindowStateNormalizationReason.NormalizationRequested)
            : new WindowStateNormalizationDecision(
                ShouldNormalize: false,
                Reason: WindowStateNormalizationReason.NormalizationNotRequested);
    }
}

internal static class WindowDragOperationState
{
    private static int _activeDragCount;

    internal static bool IsActive => Volatile.Read(ref _activeDragCount) > 0;

    internal static IDisposable Begin()
    {
        Interlocked.Increment(ref _activeDragCount);
        return InteropAdapterScope.Create(End);
    }

    private static void End()
    {
        var next = Interlocked.Decrement(ref _activeDragCount);
        if (next < 0)
        {
            Interlocked.Exchange(ref _activeDragCount, 0);
        }
    }
}

internal static class WindowExecutionPolicies
{
    internal static UserInitiatedWindowActivationDecision ResolveUserInitiatedWindowActivation(
        bool windowVisible,
        bool windowActive)
    {
        if (!windowVisible)
        {
            return new UserInitiatedWindowActivationDecision(
                ShouldActivateAfterShow: false,
                Reason: UserInitiatedWindowActivationReason.WindowNotVisible);
        }

        if (windowActive)
        {
            return new UserInitiatedWindowActivationDecision(
                ShouldActivateAfterShow: false,
                Reason: UserInitiatedWindowActivationReason.WindowAlreadyActive);
        }

        return new UserInitiatedWindowActivationDecision(
            ShouldActivateAfterShow: true,
            Reason: UserInitiatedWindowActivationReason.ActivationRequired);
    }

    internal static bool ShouldActivateAfterShow(bool windowVisible, bool windowActive)
    {
        return ResolveUserInitiatedWindowActivation(windowVisible, windowActive).ShouldActivateAfterShow;
    }

    internal static WindowLifecycleSubscriptionDecision ResolveWindowLifecycleSubscription(object? previousWindow, object? currentWindow)
    {
        if (currentWindow == null)
        {
            return new WindowLifecycleSubscriptionDecision(
                ShouldWire: false,
                Reason: WindowLifecycleSubscriptionReason.CurrentWindowMissing);
        }

        return ReferenceEquals(previousWindow, currentWindow)
            ? new WindowLifecycleSubscriptionDecision(
                ShouldWire: false,
                Reason: WindowLifecycleSubscriptionReason.SameWindowInstance)
            : new WindowLifecycleSubscriptionDecision(
                ShouldWire: true,
                Reason: WindowLifecycleSubscriptionReason.WindowInstanceChanged);
    }

    internal static bool ShouldWire(object? previousWindow, object? currentWindow)
    {
        return ResolveWindowLifecycleSubscription(previousWindow, currentWindow).ShouldWire;
    }

    internal static WindowCursorHitTestDecision ResolveWindowCursorHitTest(
        int cursorX,
        int cursorY,
        int left,
        int top,
        int right,
        int bottom)
    {
        var inside = cursorX >= left
            && cursorX <= right
            && cursorY >= top
            && cursorY <= bottom;
        return inside
            ? new WindowCursorHitTestDecision(
                IsInside: true,
                Reason: WindowCursorHitTestReason.InsideBounds)
            : new WindowCursorHitTestDecision(
                IsInside: false,
                Reason: WindowCursorHitTestReason.OutsideBounds);
    }

    internal static bool IsInside(
        int cursorX,
        int cursorY,
        int left,
        int top,
        int right,
        int bottom)
    {
        return ResolveWindowCursorHitTest(cursorX, cursorY, left, top, right, bottom).IsInside;
    }
}
