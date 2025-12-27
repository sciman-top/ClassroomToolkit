namespace ClassroomToolkit.Interop.Presentation;

public static class PresentationWindowFocus
{
    public static bool EnsureForeground(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == hwnd)
        {
            return true;
        }
        return NativeMethods.SetForegroundWindow(hwnd);
    }
}
