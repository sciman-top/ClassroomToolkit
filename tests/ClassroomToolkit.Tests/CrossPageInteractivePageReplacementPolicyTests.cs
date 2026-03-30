using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractivePageReplacementPolicyTests
{
    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenTargetMissing()
    {
        CrossPageInteractivePageReplacementPolicy.ShouldReplace(
            hasResolvedTargetFrame: false,
            interactionActive: false,
            slotPageChanged: true,
            hasCurrentFrame: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnFalse_ForStableSlotDuringInteraction()
    {
        CrossPageInteractivePageReplacementPolicy.ShouldReplace(
            hasResolvedTargetFrame: true,
            interactionActive: true,
            slotPageChanged: false,
            hasCurrentFrame: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenSlotChangedDuringInteraction()
    {
        CrossPageInteractivePageReplacementPolicy.ShouldReplace(
            hasResolvedTargetFrame: true,
            interactionActive: true,
            slotPageChanged: true,
            hasCurrentFrame: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenNoCurrentFrame()
    {
        CrossPageInteractivePageReplacementPolicy.ShouldReplace(
            hasResolvedTargetFrame: true,
            interactionActive: true,
            slotPageChanged: false,
            hasCurrentFrame: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldReuseCurrentFrame_ShouldReturnFalse_WhenSlotChanged()
    {
        CrossPageInteractivePageReplacementPolicy.ShouldReuseCurrentFrame(
            shouldReplacePageFrame: false,
            slotPageChanged: true,
            hasCurrentFrame: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldReuseCurrentFrame_ShouldReturnTrue_WhenSlotStableAndTargetNotReplaced()
    {
        CrossPageInteractivePageReplacementPolicy.ShouldReuseCurrentFrame(
            shouldReplacePageFrame: false,
            slotPageChanged: false,
            hasCurrentFrame: true).Should().BeTrue();
    }
}
