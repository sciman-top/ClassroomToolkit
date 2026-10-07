using
System.Runtime.InteropServices;

namespace ClassroomToolkit.Interop.Presentation;

public enum PresentationType
{
    None = 0,
    Wps,
    Office,
    Other
}

public sealed record PresentationWindowInfo(uint ProcessId, string ProcessName, IReadOnlyList<string> ClassNames)
{
    public static PresentationWindowInfo FromProcess(string processName)
    {
        return new PresentationWindowInfo(0, processName, Array.Empty<string>());
    }
}

public sealed record PresentationTarget(IntPtr Handle, PresentationWindowInfo Info)
{
    public bool IsValid => Handle != IntPtr.Zero;

    public static PresentationTarget Empty => new(IntPtr.Zero, PresentationWindowInfo.FromProcess(string.Empty));

    public override string ToString()
    {
        if (Handle == IntPtr.Zero)
        {
            return "(none)";
        }
        return $"0x{Handle.ToInt64():X}";
    }
}

public sealed record PresentationWindowCheck(
    PresentationType Type,
    uint ProcessId,
    string ProcessName,
    IReadOnlyList<string> ClassNames,
    bool ClassMatch,
    bool ProcessMatch,
    bool HasCaption,
    bool IsFullscreen,
    int Score);

public interface IPresentationTargetResolver
{
    PresentationTarget ResolveForeground();

    PresentationTarget ResolvePresentationTarget(
        PresentationClassifier classifier,
        bool allowWps,
        bool allowOffice,
        uint? excludeProcessId = null);
}

public sealed record PresentationWindowScoringOptions(
    int ClassMatchWeight,
    int ProcessMatchWeight,
    int NoCaptionWeight,
    int IsFullscreenWeight,
    int FullscreenClassMatchBonus,
    bool RequireClassMatchOrFullscreen,
    int MinimumCandidateScore)
{
    public static PresentationWindowScoringOptions Default { get; } = new(
        ClassMatchWeight: 10,
        ProcessMatchWeight: 3,
        NoCaptionWeight: 1,
        IsFullscreenWeight: 2,
        FullscreenClassMatchBonus: 100,
        RequireClassMatchOrFullscreen: true,
        MinimumCandidateScore: 1);
}
