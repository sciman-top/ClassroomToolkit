using System;

namespace ClassroomToolkit.App.Windowing;

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
