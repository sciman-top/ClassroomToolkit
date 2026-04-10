namespace ClassroomToolkit.Interop.Presentation;

public sealed partial class Win32PresentationResolver
{
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

        var wpsTarget = PresentationTarget.Empty;
        var officeTarget = PresentationTarget.Empty;
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
                if (info.ProcessName.Contains("powerpnt", StringComparison.OrdinalIgnoreCase))
                {
                    DebugPptWindowBeforeCheck(info.ClassNames);
                }

                var check = BuildWindowCheck(hwnd, info, classifier);
                if (check == null)
                {
                    return true;
                }
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

        var bestTarget = PresentationTarget.Empty;
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
}
