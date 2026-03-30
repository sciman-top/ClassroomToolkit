using System;

namespace ClassroomToolkit.App.Windowing;

internal interface ICursorWindowGeometryInteropAdapter
{
    bool TryGetCursorPos(out int x, out int y);

    bool TryGetWindowRect(IntPtr hwnd, out int left, out int top, out int right, out int bottom);
}
