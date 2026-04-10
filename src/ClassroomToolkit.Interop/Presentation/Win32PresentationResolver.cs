using ClassroomToolkit.Interop;

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
        if (!OperatingSystem.IsWindows() || hwnd == IntPtr.Zero)
        {
            return null;
        }
        var info = BuildWindowInfo(hwnd);
        return BuildWindowCheck(hwnd, info, classifier);
    }

}
