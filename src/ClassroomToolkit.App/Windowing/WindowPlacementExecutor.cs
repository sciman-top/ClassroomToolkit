using System;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowPlacementExecutor
{
    private static IWindowPlacementInteropAdapter _interopAdapter = new NativeWindowPlacementInteropAdapter();

    internal static IDisposable PushInteropAdapterForTest(IWindowPlacementInteropAdapter adapter)
    {
        var previous = _interopAdapter;
        _interopAdapter = adapter;
        return InteropAdapterScope.Create(() => _interopAdapter = previous);
    }

    internal static bool TryRefreshFrame(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        return TrySetWindowPosWithRetry(
            hwnd,
            IntPtr.Zero,
            0,
            0,
            0,
            0,
            WindowPlacementBitMasks.SwpNoMove
            | WindowPlacementBitMasks.SwpNoSize
            | WindowPlacementBitMasks.SwpNoZOrder
            | WindowPlacementBitMasks.SwpNoOwnerZOrder
            | WindowPlacementBitMasks.SwpFrameChanged);
    }

    internal static bool TryApplyBoundsNoActivateNoZOrder(
        IntPtr hwnd,
        int x,
        int y,
        int width,
        int height,
        bool showWindow)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        var flags = (uint)(WindowPlacementBitMasks.SwpNoActivate
            | WindowPlacementBitMasks.SwpNoZOrder
            | WindowPlacementBitMasks.SwpFrameChanged
            | WindowPlacementBitMasks.SwpNoOwnerZOrder);
        if (showWindow)
        {
            flags |= WindowPlacementBitMasks.SwpShowWindow;
        }

        return TrySetWindowPosWithRetry(hwnd, IntPtr.Zero, x, y, width, height, flags);
    }

    private static bool TrySetWindowPosWithRetry(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags)
    {
        return WindowInteropRetryExecutor.Execute(
            _ =>
            {
                var success = _interopAdapter.TrySetWindowPos(
                    hwnd,
                    insertAfter,
                    x,
                    y,
                    width,
                    height,
                    flags,
                    out var errorCode);
                return (success, errorCode);
            },
            (attempt, errorCode) => WindowPlacementInteropRetryPolicy.Resolve(attempt, errorCode).ShouldRetry);
    }
}
