using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborPageFramePolicyTests
{
    [Fact]
    public void Resolve_ShouldNotHoldOrCollapse_WhenTargetFrameResolved()
    {
        var decision = CrossPageNeighborPageFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentFrame: true,
            hasResolvedTargetFrame: true,
            interactionActive: true);

        decision.HoldCurrentFrame.Should().BeFalse();
        decision.CollapseSlot.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldCollapse_WhenSlotChangedAndTargetFrameMissing()
    {
        var decision = CrossPageNeighborPageFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentFrame: true,
            hasResolvedTargetFrame: false,
            interactionActive: false);

        decision.HoldCurrentFrame.Should().BeFalse();
        decision.CollapseSlot.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldHoldCurrent_WhenInteractionActiveAndCurrentFrameExists()
    {
        var decision = CrossPageNeighborPageFramePolicy.Resolve(
            slotPageChanged: false,
            hasCurrentFrame: true,
            hasResolvedTargetFrame: false,
            interactionActive: true);

        decision.HoldCurrentFrame.Should().BeTrue();
        decision.CollapseSlot.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldCollapse_WhenNoTargetFrameAndNoCurrentFrame()
    {
        var decision = CrossPageNeighborPageFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentFrame: false,
            hasResolvedTargetFrame: false,
            interactionActive: true);

        decision.HoldCurrentFrame.Should().BeFalse();
        decision.CollapseSlot.Should().BeTrue();
    }
}
