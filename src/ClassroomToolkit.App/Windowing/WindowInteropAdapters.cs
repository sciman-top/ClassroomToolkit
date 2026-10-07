using
ClassroomToolkit.Interop.Presentation;
using
ClassroomToolkit.Interop;
using
System.Collections.Generic;
using
System.Runtime.InteropServices;
using
System;

namespace ClassroomToolkit.App.Windowing;

internal interface ICursorWindowGeometryInteropAdapter
{
    bool TryGetCursorPos(out int x, out int y);

    bool TryGetWindowRect(IntPtr hwnd, out int left, out int top, out int right, out int bottom);
}

internal interface IWindowPlacementInteropAdapter
{
    bool TrySetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags,
        out int errorCode);
}

internal interface IWindowStyleInteropAdapter
{
    bool TryGetWindowLong(IntPtr hwnd, int index, out int style, out int errorCode);

    bool TrySetWindowLong(IntPtr hwnd, int index, int value, out int errorCode);
}

internal interface IWindowTopmostInteropAdapter
{
    bool TrySetTopmostNoActivate(IntPtr hwnd, bool enabled, out int errorCode);

    bool TrySetWindowBehindNoActivate(IntPtr hwnd, IntPtr insertAfter, out int errorCode);
}

public interface IWindowOrchestrator
{
    bool TouchSurface(IList<ZOrderSurface> surfaceStack, ZOrderSurface surface);

    void PruneSurfaceStack(
        IList<ZOrderSurface> surfaceStack,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible);

    ZOrderSurface ResolveFrontSurface(
        IReadOnlyList<ZOrderSurface> surfaceStack,
        bool photoActive,
        bool presentationFullscreen,
        bool whiteboardActive,
        bool imageManagerVisible);
}

internal static class WindowHandleValidationInteropAdapter
{
    internal static bool IsValid(IntPtr hwnd)
    {
        return hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd);
    }
}

internal sealed class NativeWindowPlacementInteropAdapter : IWindowPlacementInteropAdapter
{
    public bool TrySetWindowPos(
        IntPtr hwnd,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags,
        out int errorCode)
    {
        var success = NativeMethods.SetWindowPos(hwnd, insertAfter, x, y, width, height, flags);
        errorCode = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }
}

internal sealed class NativeWindowStyleInteropAdapter : IWindowStyleInteropAdapter
{
    public bool TryGetWindowLong(IntPtr hwnd, int index, out int style, out int errorCode)
    {
        style = NativeMethods.GetWindowLong(hwnd, index);
        errorCode = Marshal.GetLastPInvokeError();
        return IsCallSuccessful(style, errorCode);
    }

    public bool TrySetWindowLong(IntPtr hwnd, int index, int value, out int errorCode)
    {
        var previous = NativeMethods.SetWindowLong(hwnd, index, value);
        errorCode = Marshal.GetLastPInvokeError();
        return IsCallSuccessful(previous, errorCode);
    }

    /// <summary>
    /// GetWindowLong/SetWindowLong 的返回值 0 是合法值（如样式位恰好为 0），而
    /// SetLastError=true 的封送 stub 会在调用后覆写 last-error，user32 成功时又
    /// 不重置它——线程上此前失败的 Win32 调用会残留非零值。因此失败判定必须是
    /// "返回值为 0 且 last-error 非零"的组合，不能只看 last-error。
    /// </summary>
    internal static bool IsCallSuccessful(int returnValue, int errorCode)
    {
        return returnValue != 0 || errorCode == 0;
    }
}

internal sealed class NativeCursorWindowGeometryInteropAdapter : ICursorWindowGeometryInteropAdapter
{
    public bool TryGetCursorPos(out int x, out int y)
    {
        if (!NativeMethods.GetCursorPos(out var point))
        {
            x = 0;
            y = 0;
            return false;
        }

        x = point.X;
        y = point.Y;
        return true;
    }

    public bool TryGetWindowRect(IntPtr hwnd, out int left, out int top, out int right, out int bottom)
    {
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            left = 0;
            top = 0;
            right = 0;
            bottom = 0;
            return false;
        }

        left = rect.Left;
        top = rect.Top;
        right = rect.Right;
        bottom = rect.Bottom;
        return true;
    }
}

internal sealed class NativeWindowTopmostInteropAdapter : IWindowTopmostInteropAdapter
{
    public bool TrySetTopmostNoActivate(IntPtr hwnd, bool enabled, out int errorCode)
    {
        var insertAfter = enabled ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost;
        var success = NativeMethods.SetWindowPos(
            hwnd,
            insertAfter,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoActivate
            | NativeMethods.SwpNoOwnerZOrder);

        errorCode = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }

    public bool TrySetWindowBehindNoActivate(IntPtr hwnd, IntPtr insertAfter, out int errorCode)
    {
        var success = NativeMethods.SetWindowPos(
            hwnd,
            insertAfter,
            0,
            0,
            0,
            0,
            NativeMethods.SwpNoMove
            | NativeMethods.SwpNoSize
            | NativeMethods.SwpNoActivate
            | NativeMethods.SwpNoOwnerZOrder);

        errorCode = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }
}

internal static class InteropAdapterScope
{
    internal static IDisposable Create(Action restore)
    {
        ArgumentNullException.ThrowIfNull(restore);
        return new Scope(restore);
    }

    private sealed class Scope : IDisposable
    {
        private readonly Action _restore;
        private bool _disposed;

        internal Scope(Action restore)
        {
            ArgumentNullException.ThrowIfNull(restore);
            _restore = restore;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _restore();
        }
    }
}

internal static class PresentationForegroundSuppressionInteropAdapter
{
    internal static IDisposable SuppressForeground()
    {
        return PresentationWindowFocus.SuppressForeground();
    }

    internal static bool EnsureForeground(IntPtr hwnd)
    {
        return PresentationWindowFocus.EnsureForeground(hwnd);
    }

    internal static bool IsForeground(IntPtr hwnd)
    {
        return PresentationWindowFocus.IsForeground(hwnd);
    }
}

internal static class WindowStyleIndexPolicy
{
    // Win32 GWL_EXSTYLE index
    internal const int ExStyle = -20;
}

internal static class WindowStyleBitMasks
{
    internal const int GwlStyle = -16;
    internal const int GwlExStyle = -20;
    internal const int WsExTransparent = 0x00000020;
    internal const int WsExNoActivate = 0x08000000;
    internal const int WsExToolWindow = 0x00000080;
    internal const int WsMinimizeBox = 0x00020000;
}

internal static class WindowPlacementBitMasks
{
    internal const int SwpNoSize = 0x0001;
    internal const int SwpNoMove = 0x0002;
    internal const int SwpNoZOrder = 0x0004;
    internal const int SwpFrameChanged = 0x0020;
    internal const int SwpShowWindow = 0x0040;
    internal const int SwpNoActivate = 0x0010;
    internal const int SwpNoOwnerZOrder = 0x0200;
}
