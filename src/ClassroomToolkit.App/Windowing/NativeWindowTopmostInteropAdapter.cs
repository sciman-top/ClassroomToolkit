using System;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.App.Windowing;

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
}
