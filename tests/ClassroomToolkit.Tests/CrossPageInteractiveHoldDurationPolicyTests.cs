using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveHoldDurationPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(5, 0)]
    public void ResolveMs_ShouldScaleWithVisibleNeighbors(int visibleNeighbors, int expectedMs)
    {
        var ms = CrossPageInteractiveHoldDurationPolicy.ResolveMs(visibleNeighbors);
        ms.Should().Be(expectedMs);
    }

    [Fact]
    public void ResolveMs_ShouldClampToAtLeastOne()
    {
        var ms = CrossPageInteractiveHoldDurationPolicy.ResolveMs(
            visibleNeighborPages: -5,
            mode: PaintToolMode.Brush,
            baseMs: -100,
            extraPerNeighborMs: -20,
            maxMs: 0);

        ms.Should().Be(1);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForBrushMode()
    {
        var cursorMs = CrossPageInteractiveHoldDurationPolicy.ResolveMs(
            visibleNeighborPages: 2,
            mode: PaintToolMode.Cursor);
        var brushMs = CrossPageInteractiveHoldDurationPolicy.ResolveMs(
            visibleNeighborPages: 2,
            mode: PaintToolMode.Brush);

        cursorMs.Should().Be(0);
        brushMs.Should().BeGreaterThan(cursorMs);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForEraserMode()
    {
        var cursorMs = CrossPageInteractiveHoldDurationPolicy.ResolveMs(
            visibleNeighborPages: 2,
            mode: PaintToolMode.Cursor);
        var eraserMs = CrossPageInteractiveHoldDurationPolicy.ResolveMs(
            visibleNeighborPages: 2,
            mode: PaintToolMode.Eraser);

        cursorMs.Should().Be(0);
        eraserMs.Should().BeGreaterThan(cursorMs);
    }
}
