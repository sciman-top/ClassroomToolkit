using System;

namespace ClassroomToolkit.App.Windowing;

internal static class PresentationForegroundSuppressionInteropAdapter
{
    internal static IDisposable SuppressForeground()
    {
        return PresentationWindowFocus.SuppressForeground();
    }

    internal static bool EnsureForeground(IntPtr hwnd)
    {
        return PresentationWindowFocus.EnsureForeground(hwnd);
    }

    internal static bool IsForeground(IntPtr hwnd)
    {
        return PresentationWindowFocus.IsForeground(hwnd);
    }
}
