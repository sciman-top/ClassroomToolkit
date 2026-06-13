using System;

namespace ClassroomToolkit.App.Windowing;

internal interface IWindowTopmostInteropAdapter
{
    bool TrySetTopmostNoActivate(IntPtr hwnd, bool enabled, out int errorCode);

    bool TrySetWindowBehindNoActivate(IntPtr hwnd, IntPtr insertAfter, out int errorCode);
}
