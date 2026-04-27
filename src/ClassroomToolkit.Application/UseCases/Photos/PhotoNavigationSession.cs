using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace ClassroomToolkit.Application.UseCases.Photos;

public sealed class PhotoNavigationSession
{
    private List<string> _sequence = new();

    public IReadOnlyList<string> Sequence => _sequence;
    public int CurrentIndex { get; private set; } = -1;

    public void Reset(IReadOnlyList<string> sequence, int index)
    {
        _sequence = sequence?.ToList() ?? new List<string>();
        CurrentIndex = index;
    }

    [SuppressMessage("Design", "CA1024:Use properties where appropriate", Justification = "Keep method-shaped API for existing UI call sites and navigation semantics.")]
    public string? GetCurrentPath()
    {
        if (CurrentIndex < 0 || CurrentIndex >= _sequence.Count)
        {
            return null;
        }

        return _sequence[CurrentIndex];
    }

    public PhotoNavigationDecision Plan(string? overlayPath, int direction, PhotoFileType currentFileTypeHint = PhotoFileType.Unknown)
    {
        return PhotoNavigationPlanner.Plan(new PhotoNavigationRequest(
            Sequence: _sequence,
            CurrentIndex: CurrentIndex,
            CurrentPath: overlayPath,
            Direction: direction,
            CurrentFileTypeHint: currentFileTypeHint));
    }

    public void SyncResolvedIndex(PhotoNavigationDecision decision)
    {
        if (decision.ResolvedCurrentIndex >= 0)
        {
            CurrentIndex = decision.ResolvedCurrentIndex;
        }
    }

    public bool TryApplyFileNavigation(PhotoNavigationDecision decision, out string? nextPath)
    {
        nextPath = null;
        if (!decision.ShouldNavigateFile)
        {
            return false;
        }

        if (decision.NextIndex < 0 || decision.NextIndex >= _sequence.Count)
        {
            return false;
        }

        CurrentIndex = decision.NextIndex;
        nextPath = _sequence[CurrentIndex];
        return true;
    }
}
