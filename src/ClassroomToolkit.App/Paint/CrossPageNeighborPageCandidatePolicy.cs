using System;
using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborPageCandidatePolicy
{
    internal static bool ShouldUseCandidate(
        Visibility visibility,
        bool hasBitmap,
        bool hasCandidatePage,
        int candidatePage,
        int currentPage,
        bool hasRect,
        bool pointerInsideRect)
    {
        if (visibility != Visibility.Visible || !hasBitmap || !hasCandidatePage)
        {
            return false;
        }
        if (Math.Abs(candidatePage - currentPage) > 1 || !hasRect)
        {
            return false;
        }
        return pointerInsideRect;
    }
}
