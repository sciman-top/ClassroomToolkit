using
ClassroomToolkit.Interop;
using
System.Collections.Generic;
using
System.Diagnostics;

namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class Win32PresentationResolver : IPresentationTargetResolver
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

    public PresentationWindowCheck? CheckWindow(IntPtr hwnd, PresentationClassifier classifier)
    {
        ArgumentNullException.ThrowIfNull(classifier);

        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
        {
            return null;
        }
        var info = BuildWindowInfo(hwnd);
        return BuildWindowCheck(hwnd, info, classifier);
    }

}

public sealed partial class Win32PresentationResolver
{
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
        if (_scoringOptions.RequireClassMatchOrFullscreen && !classMatch && !isFullscreen)
        {
            return null;
        }

        var score = 0;
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
            info.ProcessId,
            info.ProcessName,
            info.ClassNames,
            classMatch,
            processMatch,
            hasCaption,
            isFullscreen,
            score);
    }
}

public sealed partial class Win32PresentationResolver
{
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
    private static void DebugFinalSelection(
        bool wpsValid,
        int wpsScore,
        bool officeValid,
        int officeScore)
    {
        Debug.WriteLine(
            $"[Resolver] Final: wpsValid={wpsValid}, wpsScore={wpsScore}, "
            + $"officeValid={officeValid}, officeScore={officeScore}");
    }
}
