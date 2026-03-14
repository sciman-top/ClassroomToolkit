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

    internal static bool TryApplyHandleNoActivate(IntPtr hwnd, bool enabled)
    {
        if (hwnd == IntPtr.Zero)
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
}
