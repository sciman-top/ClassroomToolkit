using System.Collections.Generic;
using System.Diagnostics;

namespace ClassroomToolkit.Interop.Presentation;

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
    private static void DebugFinalSelection(bool wpsValid, bool officeValid, int officeScore)
    {
        Debug.WriteLine($"[Resolver] Final: wpsValid={wpsValid}, officeValid={officeValid}, officeScore={officeScore}");
    }
}
