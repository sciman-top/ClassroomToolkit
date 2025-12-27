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
        var info = BuildWindowInfo(hwnd);
        return new PresentationTarget(hwnd, info);
    }

    public PresentationTarget ResolvePresentationTarget(
        PresentationClassifier classifier,
        bool allowWps,
        bool allowOffice,
        uint? excludeProcessId = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return PresentationTarget.Empty;
        }
        PresentationTarget wpsTarget = PresentationTarget.Empty;
        PresentationTarget officeTarget = PresentationTarget.Empty;
        NativeMethods.EnumWindows(
            (hwnd, _) =>
            {
                if (hwnd == IntPtr.Zero || !NativeMethods.IsWindowVisible(hwnd))
                {
                    return true;
                }
                var info = BuildWindowInfo(hwnd);
                if (excludeProcessId.HasValue && info.ProcessId == excludeProcessId.Value)
                {
                    return true;
                }
                var type = classifier.Classify(info);
                if (type == PresentationType.Wps && allowWps && !wpsTarget.IsValid)
                {
                    wpsTarget = new PresentationTarget(hwnd, info);
                }
                else if (type == PresentationType.Office && allowOffice && !officeTarget.IsValid)
                {
                    officeTarget = new PresentationTarget(hwnd, info);
                }
                return true;
            },
            IntPtr.Zero);

        if (wpsTarget.IsValid)
        {
            return wpsTarget;
        }
        if (officeTarget.IsValid)
        {
            return officeTarget;
        }
        return PresentationTarget.Empty;
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

    private static PresentationWindowInfo BuildWindowInfo(IntPtr hwnd)
    {
        var classNames = BuildClassNames(hwnd);
        var processId = GetProcessId(hwnd);
        var processName = GetProcessName(processId);
        return new PresentationWindowInfo(processId, processName, classNames);
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
