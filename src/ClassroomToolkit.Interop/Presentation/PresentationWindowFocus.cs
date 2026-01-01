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
        // 移除前台窗口检查，始终调用 SetForegroundWindow
        // WPS 需要重新调用才能完全激活内部输入上下文
        return NativeMethods.SetForegroundWindow(hwnd);
    }

    public static bool IsForeground(IntPtr hwnd)
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
        return foreground != IntPtr.Zero && foreground == hwnd;
    }
}
