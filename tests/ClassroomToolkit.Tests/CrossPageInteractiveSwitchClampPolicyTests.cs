using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveSwitchClampPolicyTests
{
    [Fact]
    public void ClampTranslateY_ShouldClampToUpperBound_WhenCandidateExceedsRange()
    {
        double Height(int _) => 200;

        var clamped = CrossPageInteractiveSwitchClampPolicy.ClampTranslateY(
            candidateTranslateY: 900,
            targetPage: 3,
            totalPages: 3,
            viewportHeight: 500,
            getPageHeight: Height,
            fallbackPageHeight: 200);

        clamped.Should().BeApproximately(400, 0.001);
    }

    [Fact]
    public void ClampTranslateY_ShouldClampToLowerBound_WhenCandidateBelowRange()
    {
        double Height(int _) => 300;

        var clamped = CrossPageInteractiveSwitchClampPolicy.ClampTranslateY(
            candidateTranslateY: -900,
            targetPage: 2,
            totalPages: 4,
            viewportHeight: 400,
            getPageHeight: Height,
            fallbackPageHeight: 300);

        clamped.Should().BeApproximately(-500, 0.001);
    }

    [Fact]
    public void ClampTranslateY_ShouldUseFallbackHeight_WhenResolverReturnsZero()
    {
        double Height(int page) => page == 2 ? 260 : 0;

        var clamped = CrossPageInteractiveSwitchClampPolicy.ClampTranslateY(
            candidateTranslateY: 700,
            targetPage: 2,
            totalPages: 3,
            viewportHeight: 400,
            getPageHeight: Height,
            fallbackPageHeight: 250);

        // above=250, target=260, below=250 => max=250, min=-110
        clamped.Should().BeApproximately(250, 0.001);
    }
}
