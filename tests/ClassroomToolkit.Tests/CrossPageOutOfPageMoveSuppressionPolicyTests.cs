using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageOutOfPageMoveSuppressionPolicyTests
{
    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenBrushStrokeIsOutsideCurrentPageInCrossPageMode()
    {
        var suppress = CrossPageOutOfPageMoveSuppressionPolicy.ShouldSuppress(
            crossPageDisplayActive: true,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppress.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenPointerStillInsideCurrentPage()
    {
        var suppress = CrossPageOutOfPageMoveSuppressionPolicy.ShouldSuppress(
            crossPageDisplayActive: true,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: true);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenNotBrushOrNotStrokeInProgress()
    {
        var suppressByTool = CrossPageOutOfPageMoveSuppressionPolicy.ShouldSuppress(
            crossPageDisplayActive: true,
            mode: PaintToolMode.Eraser,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);
        var suppressByState = CrossPageOutOfPageMoveSuppressionPolicy.ShouldSuppress(
            crossPageDisplayActive: true,
            mode: PaintToolMode.Brush,
            strokeInProgress: false,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppressByTool.Should().BeFalse();
        suppressByState.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenSwitchHappenedThisFrame()
    {
        var suppress = CrossPageOutOfPageMoveSuppressionPolicy.ShouldSuppress(
            crossPageDisplayActive: true,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: true,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenInRecentSwitchGraceWindow()
    {
        var suppress = CrossPageOutOfPageMoveSuppressionPolicy.ShouldSuppress(
            crossPageDisplayActive: true,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: true,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppress.Should().BeFalse();
    }
}
