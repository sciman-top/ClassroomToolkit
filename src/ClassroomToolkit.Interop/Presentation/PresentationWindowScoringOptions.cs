namespace ClassroomToolkit.Interop.Presentation;

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
