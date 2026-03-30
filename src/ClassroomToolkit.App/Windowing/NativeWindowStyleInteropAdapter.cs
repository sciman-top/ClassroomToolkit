global using ClassroomToolkit.Interop.Presentation;
global using ClassroomToolkit.Interop;
global using NativeMethods = ClassroomToolkit.Interop.NativeMethods;
global using MonitorInfo = ClassroomToolkit.Interop.NativeMethods.MonitorInfo;

using System;
using System.Runtime.InteropServices;

namespace ClassroomToolkit.App.Windowing;

internal sealed class NativeWindowStyleInteropAdapter : IWindowStyleInteropAdapter
{
    public bool TryGetWindowLong(IntPtr hwnd, int index, out int style, out int errorCode)
    {
        Marshal.SetLastPInvokeError(0);
        style = NativeMethods.GetWindowLong(hwnd, index);
        errorCode = Marshal.GetLastPInvokeError();
        // GetWindowLong may return 0 legitimately; treat error only when last-error is non-zero.
        return errorCode == 0;
    }

    public bool TrySetWindowLong(IntPtr hwnd, int index, int value, out int errorCode)
    {
        Marshal.SetLastPInvokeError(0);
        _ = NativeMethods.SetWindowLong(hwnd, index, value);
        errorCode = Marshal.GetLastPInvokeError();
        return errorCode == 0;
    }
}
