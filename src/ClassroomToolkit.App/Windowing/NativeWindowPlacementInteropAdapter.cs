using System;
using System.Runtime.InteropServices;
using ClassroomToolkit.Interop;

namespace ClassroomToolkit.App.Windowing;

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
