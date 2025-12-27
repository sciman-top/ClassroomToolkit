using System.Diagnostics;
using System.Runtime.InteropServices;
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
        var wpsScore = -1;
        var officeScore = -1;
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
                var check = BuildWindowCheck(hwnd, info, classifier);
                if (check == null)
                {
                    return true;
                }
                if (check.Type == PresentationType.Wps && allowWps && check.Score > wpsScore)
                {
                    wpsScore = check.Score;
                    wpsTarget = new PresentationTarget(hwnd, info);
                }
                else if (check.Type == PresentationType.Office && allowOffice && check.Score > officeScore)
                {
                    officeScore = check.Score;
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

    public PresentationWindowCheck? CheckWindow(IntPtr hwnd, PresentationClassifier classifier)
    {
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
        {
            return null;
        }
        var info = BuildWindowInfo(hwnd);
        return BuildWindowCheck(hwnd, info, classifier);
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

    private static PresentationWindowCheck? BuildWindowCheck(
        IntPtr hwnd,
        PresentationWindowInfo info,
        PresentationClassifier classifier)
    {
        if (info == null)
        {
            return null;
        }
        var type = classifier.Classify(info);
        if (type is PresentationType.None or PresentationType.Other)
        {
            return null;
        }
        var classMatch = classifier.IsSlideshowWindow(info);
        var processMatch = type is PresentationType.Wps or PresentationType.Office;
        var hasCaption = HasCaption(hwnd);
        var isFullscreen = IsFullscreenWindow(hwnd);
        if (!classMatch && !isFullscreen && hasCaption)
        {
            return null;
        }
        var score = 0;
        if (classMatch)
        {
            score += 3;
        }
        if (processMatch)
        {
            score += 3;
        }
        if (!hasCaption)
        {
            score += 1;
        }
        if (isFullscreen)
        {
            score += 2;
        }
        return new PresentationWindowCheck(
            type,
            info.ProcessName,
            info.ClassNames,
            classMatch,
            processMatch,
            hasCaption,
            isFullscreen,
            score);
    }

    private static bool HasCaption(IntPtr hwnd)
    {
        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlStyle);
        return (style & NativeMethods.WsCaption) != 0;
    }

    private static bool IsFullscreenWindow(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }
        if (!NativeMethods.GetWindowRect(hwnd, out var rect))
        {
            return false;
        }
        var monitor = NativeMethods.MonitorFromWindow(hwnd, NativeMethods.MonitorDefaultToNearest);
        if (monitor == IntPtr.Zero)
        {
            return false;
        }
        var info = new NativeMethods.MonitorInfo
        {
            Size = Marshal.SizeOf<NativeMethods.MonitorInfo>()
        };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }
        const int tolerance = 2;
        return Math.Abs(rect.Left - info.Monitor.Left) <= tolerance
               && Math.Abs(rect.Top - info.Monitor.Top) <= tolerance
               && Math.Abs(rect.Right - info.Monitor.Right) <= tolerance
               && Math.Abs(rect.Bottom - info.Monitor.Bottom) <= tolerance;
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
