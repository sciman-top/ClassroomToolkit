using System;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowHandleValidationInteropAdapter
{
    internal static bool IsValid(IntPtr hwnd)
    {
        return hwnd != IntPtr.Zero && NativeMethods.IsWindow(hwnd);
    }
}
