using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveInkClearPolicyTests
{
    [Fact]
    public void ShouldClearCurrentFrame_ShouldReturnFalse_WhenInteractionIsActive()
    {
        var shouldClear = CrossPageInteractiveInkClearPolicy.ShouldClearCurrentFrame(
            holdInkReplacement: false,
            hasNeighborInkStrokes: false,
            inkOperationActive: false,
            interactionActive: true);

        shouldClear.Should().BeFalse();
    }

    [Fact]
    public void ShouldClearCurrentFrame_ShouldReturnFalse_WhenInkOperationIsActive()
    {
        var shouldClear = CrossPageInteractiveInkClearPolicy.ShouldClearCurrentFrame(
            holdInkReplacement: false,
            hasNeighborInkStrokes: false,
            inkOperationActive: true,
            interactionActive: false);

        shouldClear.Should().BeFalse();
    }

    [Fact]
    public void ShouldClearCurrentFrame_ShouldReturnFalse_WhenHoldReplacementIsTrue()
    {
        var shouldClear = CrossPageInteractiveInkClearPolicy.ShouldClearCurrentFrame(
            holdInkReplacement: true,
            hasNeighborInkStrokes: false,
            inkOperationActive: false,
            interactionActive: false);

        shouldClear.Should().BeFalse();
    }

    [Fact]
    public void ShouldClearCurrentFrame_ShouldReturnTrue_OnlyWhenStableNoHoldNoInteractionAndNoNeighborStrokes()
    {
        var shouldClear = CrossPageInteractiveInkClearPolicy.ShouldClearCurrentFrame(
            holdInkReplacement: false,
            hasNeighborInkStrokes: false,
            inkOperationActive: false,
            interactionActive: false);

        shouldClear.Should().BeTrue();
    }
}
