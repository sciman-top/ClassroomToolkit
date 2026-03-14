using System;

namespace ClassroomToolkit.App.Windowing;

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
