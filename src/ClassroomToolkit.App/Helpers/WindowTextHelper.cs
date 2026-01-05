using System;
using System.Runtime.InteropServices;
using System.Text;

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
        int length = GetWindowText(hwnd, buffer, buffer.Capacity);
        if (length <= 0)
        {
            return string.Empty;
        }
        return buffer.ToString();
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int maxCount);
}
