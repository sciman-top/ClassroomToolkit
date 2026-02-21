using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputNavigationTests
{
    [Fact]
    public void ResolveTargetPage_ShouldReturnCurrent_WhenPointerInsideCurrent()
    {
        var page = CrossPageInputNavigation.ResolveTargetPage(
            pointerY: 120,
            currentPage: 3,
            totalPages: 5,
            currentTop: 100,
            currentHeight: 200,
            getPageHeight: _ => 200);

        page.Should().Be(3);
    }

    [Fact]
    public void ResolveTargetPage_ShouldResolvePrevious_WithVariablePdfLikeHeights()
    {
        double Height(int page) => page switch
        {
            1 => 100,
            2 => 160,
            3 => 220,
            _ => 120
        };

        var page = CrossPageInputNavigation.ResolveTargetPage(
            pointerY: -30,
            currentPage: 3,
            totalPages: 4,
            currentTop: 80,
            currentHeight: 220,
            getPageHeight: Height);

        page.Should().Be(2);
    }

    [Fact]
    public void ResolveTargetPage_ShouldResolveNext_WithVariablePdfLikeHeights()
    {
        double Height(int page) => page switch
        {
            1 => 130,
            2 => 150,
            3 => 180,
            4 => 210,
            _ => 100
        };

        var page = CrossPageInputNavigation.ResolveTargetPage(
            pointerY: 430,
            currentPage: 2,
            totalPages: 4,
            currentTop: 50,
            currentHeight: 150,
            getPageHeight: Height);

        page.Should().Be(4);
    }

    [Fact]
    public void ComputePageOffset_ShouldAccumulateForwardAndBackward()
    {
        double Height(int page) => page switch
        {
            1 => 100,
            2 => 120,
            3 => 140,
            4 => 160,
            _ => 100
        };

        var forward = CrossPageInputNavigation.ComputePageOffset(2, 4, Height);
        var backward = CrossPageInputNavigation.ComputePageOffset(4, 2, Height);

        forward.Should().BeApproximately(260, 0.001);
        backward.Should().BeApproximately(-260, 0.001);
    }
}
