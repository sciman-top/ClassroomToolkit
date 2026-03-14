using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborInkReplacementPolicyTests
{
    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenNoCurrentInkFrame()
    {
        var shouldReplace = CrossPageNeighborInkReplacementPolicy.ShouldReplace(
            slotPageChanged: true,
            hasCurrentInkFrame: false,
            usedPreservedInkFrame: false);

        shouldReplace.Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenSlotUnchangedAndCurrentFrameExists()
    {
        var shouldReplace = CrossPageNeighborInkReplacementPolicy.ShouldReplace(
            slotPageChanged: false,
            hasCurrentInkFrame: true,
            usedPreservedInkFrame: false);

        shouldReplace.Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenSlotChangedButUsingPreservedFrame()
    {
        var shouldReplace = CrossPageNeighborInkReplacementPolicy.ShouldReplace(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            usedPreservedInkFrame: true);

        shouldReplace.Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenSlotChangedWithoutPreservedFrame()
    {
        var shouldReplace = CrossPageNeighborInkReplacementPolicy.ShouldReplace(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            usedPreservedInkFrame: false);

        shouldReplace.Should().BeTrue();
    }
}
