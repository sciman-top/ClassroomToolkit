using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClassroomToolkit.Interop.Utilities;

namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class Win32PresentationResolver
{
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

        // PowerPoint slideshow/annotation surfaces may keep a small non-client margin.
        // A wider tolerance avoids dropping valid fullscreen candidates in pen mode.
        const int tolerance = 16;
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
        catch (Exception ex) when (InteropExceptionFilterPolicy.IsNonFatal(ex))
        {
            return string.Empty;
        }
    }
}
