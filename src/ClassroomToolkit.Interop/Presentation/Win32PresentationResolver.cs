using System.Diagnostics;
using System.Text;

namespace ClassroomToolkit.Interop.Presentation;

public sealed class Win32PresentationResolver
{
    public PresentationTarget ResolveForeground()
    {
        if (!OperatingSystem.IsWindows())
        {
            return PresentationTarget.Empty;
        }
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return PresentationTarget.Empty;
        }
        var className = GetClassName(hwnd);
        var processName = GetProcessName(hwnd);
        var info = new PresentationWindowInfo(processName, string.IsNullOrWhiteSpace(className)
            ? Array.Empty<string>()
            : new[] { className });
        return new PresentationTarget(hwnd, info);
    }

    private static string GetClassName(IntPtr hwnd)
    {
        var builder = new StringBuilder(NativeMethods.MaxClassName);
        var length = NativeMethods.GetClassName(hwnd, builder, builder.Capacity);
        return length > 0 ? builder.ToString() : string.Empty;
    }

    private static string GetProcessName(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0)
        {
            return string.Empty;
        }
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return string.Empty;
        }
    }
}
