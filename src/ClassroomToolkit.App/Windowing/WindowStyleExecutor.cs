using System;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowStyleExecutor
{
    private const int ExtendedStyleIndex = -20;
    private static IWindowStyleInteropAdapter _interopAdapter = new NativeWindowStyleInteropAdapter();

    internal static IDisposable PushInteropAdapterForTest(IWindowStyleInteropAdapter adapter)
    {
        var previous = _interopAdapter;
        _interopAdapter = adapter;
        return InteropAdapterScope.Create(() => _interopAdapter = previous);
    }

    internal static bool TryUpdateStyleBits(
        IntPtr hwnd,
        int index,
        int setMask,
        int clearMask,
        out int updatedStyle)
    {
        updatedStyle = 0;
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        if (!TryGetStyleWithRetry(hwnd, index, out var current))
        {
            return false;
        }

        var next = (current | setMask) & ~clearMask;
        if (next == current)
        {
            updatedStyle = current;
            return true;
        }

        if (!TrySetStyleWithRetry(hwnd, index, next))
        {
            return false;
        }

        updatedStyle = next;
        return true;
    }

    internal static bool TryUpdateExtendedStyleBits(
        IntPtr hwnd,
        int setMask,
        int clearMask,
        out int updatedStyle)
    {
        return TryUpdateStyleBits(hwnd, ExtendedStyleIndex, setMask, clearMask, out updatedStyle);
    }

    private static bool TryGetStyleWithRetry(IntPtr hwnd, int index, out int style)
    {
        return WindowInteropRetryExecutor.ExecuteWithValue(
            _ =>
            {
                var success = _interopAdapter.TryGetWindowLong(hwnd, index, out var currentStyle, out var errorCode);
                return (success, currentStyle, errorCode);
            },
            (attempt, errorCode) => WindowStyleInteropRetryPolicy.Resolve(attempt, errorCode).ShouldRetry,
            out style);
    }

    private static bool TrySetStyleWithRetry(IntPtr hwnd, int index, int value)
    {
        return WindowInteropRetryExecutor.Execute(
            _ =>
            {
                var success = _interopAdapter.TrySetWindowLong(hwnd, index, value, out var errorCode);
                return (success, errorCode);
            },
            (attempt, errorCode) => WindowStyleInteropRetryPolicy.Resolve(attempt, errorCode).ShouldRetry);
    }
}
