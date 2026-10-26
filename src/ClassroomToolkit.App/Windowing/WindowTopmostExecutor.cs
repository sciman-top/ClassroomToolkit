using System;
using System.Windows;
using System.Windows.Interop;
using System.Threading;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowTopmostExecutor
{
    // WS_EX_TOPMOST (winuser.h)：HWND_TOPMOST/NOTOPMOST 维护的扩展样式位。
    private const int WsExTopmost = 0x00000008;

    private static readonly IWindowTopmostInteropAdapter DefaultInteropAdapter = new NativeWindowTopmostInteropAdapter();
    private static readonly AsyncLocal<IWindowTopmostInteropAdapter?> ScopedInteropAdapter = new();

    internal static IDisposable PushInteropAdapterForTest(IWindowTopmostInteropAdapter adapter)
    {
        var previous = ScopedInteropAdapter.Value;
        ScopedInteropAdapter.Value = adapter;
        return InteropAdapterScope.Create(() => ScopedInteropAdapter.Value = previous);
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

        // 周期性重断言（Z 序 watchdog 等）在扩展样式已处于目标层级时免一次 SetWindowPos，
        // 消除对已达标窗口的无变化重断言及其 WM_WINDOWPOSCHANGING 副作用；
        // 样式读取失败按未达标继续走原路径，保持兜底语义。
        if (WindowStyleExecutor.TryGetExtendedStyleBits(hwnd, out var currentStyle)
            && (currentStyle & WsExTopmost) == (enabled ? WsExTopmost : 0))
        {
            return true;
        }

        return WindowInteropRetryExecutor.Execute(
            _ =>
            {
                var adapter = ScopedInteropAdapter.Value ?? DefaultInteropAdapter;
                var success = adapter.TrySetTopmostNoActivate(hwnd, enabled, out var errorCode);
                return (success, errorCode);
            },
            (attempt, errorCode) => WindowInteropRetryPolicies.Resolve(attempt, errorCode).ShouldRetry);
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
                var adapter = ScopedInteropAdapter.Value ?? DefaultInteropAdapter;
                var success = adapter.TrySetWindowBehindNoActivate(hwnd, insertAfterHwnd, out var errorCode);
                return (success, errorCode);
            },
            (attempt, errorCode) => WindowInteropRetryPolicies.Resolve(attempt, errorCode).ShouldRetry);
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
