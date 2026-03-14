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
            inkOperationActive: false);

        hold.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenInteractionActiveAndInkOperationActiveAndCurrentFrameExists()
    {
        var hold = CrossPageInteractiveNeighborInkHoldPolicy.Resolve(
            baseHoldReplacement: false,
            interactionActive: true,
            hasCurrentInkFrame: true,
            inkOperationActive: true);

        hold.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenInteractionActiveButNoInkOperation()
    {
        var hold = CrossPageInteractiveNeighborInkHoldPolicy.Resolve(
            baseHoldReplacement: false,
            interactionActive: true,
            hasCurrentInkFrame: true,
            inkOperationActive: false);

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
            inkOperationActive: false);

        hold.Should().BeFalse();
    }
}
