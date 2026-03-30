using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ClassroomToolkit.Interop;
using ClassroomToolkit.Interop.Utilities;

namespace ClassroomToolkit.Interop.Presentation;

public sealed class Win32PresentationResolver : IPresentationTargetResolver
{
    private PresentationWindowScoringOptions _scoringOptions = PresentationWindowScoringOptions.Default;

    public void UpdateScoringOptions(PresentationWindowScoringOptions? options)
    {
        _scoringOptions = options ?? PresentationWindowScoringOptions.Default;
    }

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
                // Log ALL PowerPoint windows (before check filtering)
                if (info.ProcessName.Contains("powerpnt", StringComparison.OrdinalIgnoreCase))
                {
                    DebugPptWindowBeforeCheck(info.ClassNames);
                }
                var check = BuildWindowCheck(hwnd, info, classifier);
                if (check == null)
                {
                    return true;
                }
                // Log all Office candidate windows
                if (check.Type == PresentationType.Office && allowOffice)
                {
                    DebugOfficeCandidate(info.ProcessName, info.ClassNames, check.Score, check.ClassMatch, check.IsFullscreen);
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

        DebugFinalSelection(wpsTarget.IsValid, officeTarget.IsValid, officeScore);
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

    public PresentationTarget ResolveFullscreenPresentationTarget(
        PresentationClassifier classifier,
        bool allowWps,
        bool allowOffice,
        uint? excludeProcessId = null)
    {
        if (!OperatingSystem.IsWindows())
        {
            return PresentationTarget.Empty;
        }

        PresentationTarget bestTarget = PresentationTarget.Empty;
        var bestScore = int.MinValue;

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
                if (check == null || !check.IsFullscreen)
                {
                    return true;
                }

                if (check.Type == PresentationType.Wps && !allowWps)
                {
                    return true;
                }
                if (check.Type == PresentationType.Office && !allowOffice)
                {
                    return true;
                }

                // Prefer fullscreen candidates that also match slideshow classes.
                var score = check.Score + (check.ClassMatch ? _scoringOptions.FullscreenClassMatchBonus : 0);
                if (score <= bestScore)
                {
                    return true;
                }

                bestScore = score;
                bestTarget = new PresentationTarget(hwnd, info);
                return true;
            },
            IntPtr.Zero);

        return bestTarget;
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

    private PresentationWindowCheck? BuildWindowCheck(
        IntPtr hwnd,
        PresentationWindowInfo info,
        PresentationClassifier classifier)
    {
        var type = classifier.Classify(info);
        if (type is PresentationType.None or PresentationType.Other)
        {
            return null;
        }
        var classMatch = classifier.IsSlideshowWindow(info);
        var processMatch = type is PresentationType.Wps or PresentationType.Office;
        var hasCaption = HasCaption(hwnd);
        var isFullscreen = IsFullscreenWindow(hwnd);
        // Filter: require slideshow class match or fullscreen; caption only affects score.
        if (_scoringOptions.RequireClassMatchOrFullscreen && !classMatch && !isFullscreen)
        {
            return null;
        }
        var score = 0;
        // Strongly prioritize windows with slideshow class names (screenClass, pptviewwndclass, etc.)
        if (classMatch)
        {
            score += _scoringOptions.ClassMatchWeight;
        }
        if (processMatch)
        {
            score += _scoringOptions.ProcessMatchWeight;
        }
        if (!hasCaption)
        {
            score += _scoringOptions.NoCaptionWeight;
        }
        if (isFullscreen)
        {
            score += _scoringOptions.IsFullscreenWeight;
        }

        if (score < _scoringOptions.MinimumCandidateScore)
        {
            return null;
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

    [Conditional("DEBUG")]
    private static void DebugPptWindowBeforeCheck(IReadOnlyList<string> classNames)
    {
        Debug.WriteLine($"[Resolver] PPT window BEFORE check: classes={string.Join(",", classNames)}");
    }

    [Conditional("DEBUG")]
    private static void DebugOfficeCandidate(
        string processName,
        IReadOnlyList<string> classNames,
        int score,
        bool classMatch,
        bool isFullscreen)
    {
        Debug.WriteLine(
            $"[Resolver] Office candidate: process={processName}, classes={string.Join(",", classNames)}, score={score}, classMatch={classMatch}, fullscreen={isFullscreen}");
    }

    [Conditional("DEBUG")]
    private static void DebugFinalSelection(bool wpsValid, bool officeValid, int officeScore)
    {
        Debug.WriteLine($"[Resolver] Final: wpsValid={wpsValid}, officeValid={officeValid}, officeScore={officeScore}");
    }
}
