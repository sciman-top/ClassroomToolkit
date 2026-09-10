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
                if (!IsSelectableCandidate(check))
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

        DebugFinalSelection(wpsTarget.IsValid, wpsScore, officeTarget.IsValid, officeScore);
        return SelectBestTarget(wpsTarget, wpsScore, officeTarget, officeScore);
    }

    internal static PresentationTarget SelectBestTarget(
        PresentationTarget wpsTarget,
        int wpsScore,
        PresentationTarget officeTarget,
        int officeScore)
    {
        if (!wpsTarget.IsValid)
        {
            return officeTarget.IsValid ? officeTarget : PresentationTarget.Empty;
        }

        if (!officeTarget.IsValid)
        {
            return wpsTarget;
        }

        if (wpsScore > officeScore)
        {
            return wpsTarget;
        }

        if (officeScore > wpsScore)
        {
            return officeTarget;
        }

        // Equal evidence does not identify the intended application.  Returning
        // no target is safer than silently sending a page-turn to the wrong app.
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
                if (check == null || !IsSelectableCandidate(check) || !check.IsFullscreen)
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

    internal static bool IsSelectableCandidate(PresentationWindowCheck check)
    {
        ArgumentNullException.ThrowIfNull(check);

        // A generic WPS editor can score as a fullscreen WPS window.  It must
        // not shadow a real slideshow candidate before the higher-level
        // admission policy gets a chance to validate the target.
        return check.Type != PresentationType.Wps
               || check.ClassMatch
               || PresentationClassifier.IsDedicatedWpsPresentationRuntime(check.ProcessName);
    }
}
