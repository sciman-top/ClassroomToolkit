using System;
using System.Runtime.InteropServices;
using System.Text;

using ClassroomToolkit.Interop;

namespace ClassroomToolkit.App.Helpers;

public static class WindowTextHelper
{
    public static string GetWindowTitle(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return string.Empty;
        }
        var buffer = new StringBuilder(256);
        int length = NativeMethods.GetWindowText(hwnd, buffer, buffer.Capacity);
        if (length <= 0)
        {
            return string.Empty;
        }
        return buffer.ToString();
    }

}
