using System;
using System.Windows;
using System.Windows.Interop;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowTopmostExecutor
{
    private static IWindowTopmostInteropAdapter _interopAdapter = new NativeWindowTopmostInteropAdapter();

    internal static IDisposable PushInteropAdapterForTest(IWindowTopmostInteropAdapter adapter)
    {
        var previous = _interopAdapter;
        _interopAdapter = adapter;
        return InteropAdapterScope.Create(() => _interopAdapter = previous);
    }

    internal static void ApplyNoActivate(Window? window, bool enabled, bool enforceZOrder = true)
    {
        if (window == null || !window.IsVisible || window.WindowState == WindowState.Minimized)
        {
            return;
        }

        if (WindowDragOperationState.IsActive)
        {
            return;
        }

        if (window.Topmost != enabled)
        {
            window.Topmost = enabled;
        }

        if (!enforceZOrder)
        {
            return;
        }

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        TryApplyHandleNoActivate(hwnd, enabled);
    }

    internal static void PrepareNoActivateBehind(Window? window, Window? insertAfterWindow)
    {
        ApplyNoActivateBehindCore(
            window,
            insertAfterWindow,
            requireVisible: false,
            allowFallbackTopmost: false);
    }

    internal static void ApplyNoActivateBehind(Window? window, Window? insertAfterWindow)
    {
        ApplyNoActivateBehindCore(
            window,
            insertAfterWindow,
            requireVisible: true,
            allowFallbackTopmost: true);
    }

    private static void ApplyNoActivateBehindCore(
        Window? window,
        Window? insertAfterWindow,
        bool requireVisible,
        bool allowFallbackTopmost)
    {
        if (window == null || window.WindowState == WindowState.Minimized)
        {
            return;
        }

        if (requireVisible && !window.IsVisible)
        {
            return;
        }

        if (WindowDragOperationState.IsActive)
        {
            return;
        }

        var hwnd = ResolveWindowHandle(window, ensureHiddenHandle: !requireVisible);
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        var insertAfterHwnd = ResolveInsertAfterHandle(insertAfterWindow);
        if (insertAfterHwnd == IntPtr.Zero)
        {
            if (!allowFallbackTopmost)
            {
                return;
            }

            if (window.Topmost != true)
            {
                window.Topmost = true;
            }

            TryApplyHandleNoActivate(hwnd, enabled: true);
            return;
        }

        TryApplyHandleBehindNoActivate(hwnd, insertAfterHwnd);
    }

    internal static bool TryApplyHandleNoActivate(IntPtr hwnd, bool enabled)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (WindowDragOperationState.IsActive)
        {
            return false;
        }

        return WindowInteropRetryExecutor.Execute(
            _ =>
            {
                var success = _interopAdapter.TrySetTopmostNoActivate(hwnd, enabled, out var errorCode);
                return (success, errorCode);
            },
            (attempt, errorCode) => WindowTopmostInteropRetryPolicy.Resolve(attempt, errorCode).ShouldRetry);
    }

    internal static bool TryApplyHandleBehindNoActivate(IntPtr hwnd, IntPtr insertAfterHwnd)
    {
        if (hwnd == IntPtr.Zero || insertAfterHwnd == IntPtr.Zero)
        {
            return false;
        }

        if (WindowDragOperationState.IsActive)
        {
            return false;
        }

        return WindowInteropRetryExecutor.Execute(
            _ =>
            {
                var success = _interopAdapter.TrySetWindowBehindNoActivate(hwnd, insertAfterHwnd, out var errorCode);
                return (success, errorCode);
            },
            (attempt, errorCode) => WindowTopmostInteropRetryPolicy.Resolve(attempt, errorCode).ShouldRetry);
    }

    private static IntPtr ResolveInsertAfterHandle(Window? insertAfterWindow)
    {
        if (insertAfterWindow == null || !insertAfterWindow.IsVisible || insertAfterWindow.WindowState == WindowState.Minimized)
        {
            return IntPtr.Zero;
        }

        return new WindowInteropHelper(insertAfterWindow).Handle;
    }

    private static IntPtr ResolveWindowHandle(Window window, bool ensureHiddenHandle)
    {
        var helper = new WindowInteropHelper(window);
        var hwnd = helper.Handle;
        if (hwnd == IntPtr.Zero && ensureHiddenHandle)
        {
            hwnd = helper.EnsureHandle();
        }

        return hwnd;
    }
}
