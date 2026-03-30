using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborPageCandidatePolicyTests
{
    [Theory]
    [InlineData(Visibility.Collapsed, true, true, 4, 3, true, true, false)]
    [InlineData(Visibility.Visible, false, true, 4, 3, true, true, false)]
    [InlineData(Visibility.Visible, true, false, 4, 3, true, true, false)]
    [InlineData(Visibility.Visible, true, true, 6, 3, true, true, false)]
    [InlineData(Visibility.Visible, true, true, 4, 3, false, true, false)]
    [InlineData(Visibility.Visible, true, true, 4, 3, true, false, false)]
    [InlineData(Visibility.Visible, true, true, 4, 3, true, true, true)]
    [InlineData(Visibility.Visible, true, true, 2, 3, true, true, true)]
    public void ShouldUseCandidate_ShouldMatchExpected(
        Visibility visibility,
        bool hasBitmap,
        bool hasCandidatePage,
        int candidatePage,
        int currentPage,
        bool hasRect,
        bool pointerInsideRect,
        bool expected)
    {
        var result = CrossPageNeighborPageCandidatePolicy.ShouldUseCandidate(
            visibility,
            hasBitmap,
            hasCandidatePage,
            candidatePage,
            currentPage,
            hasRect,
            pointerInsideRect);

        result.Should().Be(expected);
    }
}
