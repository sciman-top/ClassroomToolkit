using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveNeighborInkHoldPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_WhenBaseHoldIsTrue()
    {
        var hold = CrossPageInteractiveNeighborInkHoldPolicy.Resolve(
            baseHoldReplacement: true,
            interactionActive: false,
            hasCurrentInkFrame: false,
            inkOperationActive: false,
            slotPageChanged: false);

        hold.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenInteractionActiveAndInkOperationActiveAndCurrentFrameExists()
    {
        var hold = CrossPageInteractiveNeighborInkHoldPolicy.Resolve(
            baseHoldReplacement: false,
            interactionActive: true,
            hasCurrentInkFrame: true,
            inkOperationActive: true,
            slotPageChanged: false);

        hold.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenInteractionActiveButNoInkOperation()
    {
        var hold = CrossPageInteractiveNeighborInkHoldPolicy.Resolve(
            baseHoldReplacement: false,
            interactionActive: true,
            hasCurrentInkFrame: true,
            inkOperationActive: false,
            slotPageChanged: false);

        hold.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void Resolve_ShouldReturnFalse_WhenNoBaseHoldAndNoCurrentFrame(
        bool interactionActive,
        bool hasCurrentInkFrame)
    {
        var hold = CrossPageInteractiveNeighborInkHoldPolicy.Resolve(
            baseHoldReplacement: false,
            interactionActive: interactionActive,
            hasCurrentInkFrame: hasCurrentInkFrame,
            inkOperationActive: false,
            slotPageChanged: false);

        hold.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, true)]
    public void Resolve_ShouldReturnFalse_WhenSlotPageChanged(
        bool baseHoldReplacement,
        bool interactionActive,
        bool hasCurrentInkFrame,
        bool inkOperationActive)
    {
        var hold = CrossPageInteractiveNeighborInkHoldPolicy.Resolve(
            baseHoldReplacement: baseHoldReplacement,
            interactionActive: interactionActive,
            hasCurrentInkFrame: hasCurrentInkFrame,
            inkOperationActive: inkOperationActive,
            slotPageChanged: true);

        hold.Should().BeFalse();
    }
}
