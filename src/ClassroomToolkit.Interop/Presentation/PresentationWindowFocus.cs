using System.Threading;

namespace ClassroomToolkit.Interop.Presentation;

public static class PresentationWindowFocus
{
    private static int _foregroundSuppressionCount;

    public static IDisposable SuppressForeground()
    {
        Interlocked.Increment(ref _foregroundSuppressionCount);
        return new ForegroundSuppression();
    }

    public static bool IsForegroundSuppressed => Volatile.Read(ref _foregroundSuppressionCount) > 0;

    public static bool EnsureForeground(IntPtr hwnd)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }
        if (IsForegroundSuppressed)
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

    private sealed class ForegroundSuppression : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
            {
                return;
            }
            var count = Interlocked.Decrement(ref _foregroundSuppressionCount);
            if (count < 0)
            {
                Interlocked.Exchange(ref _foregroundSuppressionCount, 0);
            }
        }
    }
}
