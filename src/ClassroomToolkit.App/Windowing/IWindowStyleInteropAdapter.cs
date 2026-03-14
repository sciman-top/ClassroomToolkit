using System;

namespace ClassroomToolkit.App.Windowing;

internal interface IWindowStyleInteropAdapter
{
    bool TryGetWindowLong(IntPtr hwnd, int index, out int style, out int errorCode);

    bool TrySetWindowLong(IntPtr hwnd, int index, int value, out int errorCode);
}
