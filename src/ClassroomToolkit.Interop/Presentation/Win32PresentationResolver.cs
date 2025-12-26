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
        var classNames = BuildClassNames(hwnd);
        var processId = GetProcessId(hwnd);
        var processName = GetProcessName(processId);
        var info = new PresentationWindowInfo(processId, processName, classNames);
        return new PresentationTarget(hwnd, info);
    }

    private static IReadOnlyList<string> BuildClassNames(IntPtr hwnd)
    {
        var names = new List<string>();
        AddClassName(names, hwnd);
        var root = NativeMethods.GetAncestor(hwnd, NativeMethods.GaRoot);
        if (root != IntPtr.Zero && root != hwnd)
        {
            AddClassName(names, root);
        }
        return names;
    }

    private static void AddClassName(ICollection<string> list, IntPtr hwnd)
    {
        var builder = new StringBuilder(NativeMethods.MaxClassName);
        var length = NativeMethods.GetClassName(hwnd, builder, builder.Capacity);
        if (length <= 0)
        {
            return;
        }
        var name = builder.ToString();
        if (!string.IsNullOrWhiteSpace(name))
        {
            list.Add(name);
        }
    }

    private static uint GetProcessId(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out var pid);
        return pid;
    }

    private static string GetProcessName(uint processId)
    {
        if (processId == 0)
        {
            return string.Empty;
        }
        try
        {
            using var process = Process.GetProcessById((int)processId);
            return process.ProcessName + ".exe";
        }
        catch
        {
            return string.Empty;
        }
    }
}
