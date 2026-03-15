using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborInkFramePolicyTests
{
    [Fact]
    public void Resolve_ShouldClearCurrentFrame_WhenSlotChangedWithoutResolvedInk()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            hasTargetInkStrokes: false,
            holdInkReplacement: false,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeTrue();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenHeld()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            hasTargetInkStrokes: true,
            holdInkReplacement: true,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: true);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldAllowReplacement_WhenResolvedBitmapCanReplace()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentInkFrame: false,
            hasTargetInkStrokes: true,
            holdInkReplacement: false,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: true);

        decision.ClearCurrentFrame.Should().BeTrue();
        decision.AllowResolvedInkReplacement.Should().BeTrue();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepVisible_WhenUsingPreservedFrame()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentInkFrame: false,
            hasTargetInkStrokes: true,
            holdInkReplacement: false,
            usedPreservedInkFrame: true,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldClear_WhenNoFrameAndNoPreserveAndNoHold()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentInkFrame: false,
            hasTargetInkStrokes: false,
            holdInkReplacement: false,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeTrue();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenTargetInkIsUnresolvedAndSlotDidNotChange()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: false,
            hasCurrentInkFrame: true,
            hasTargetInkStrokes: false,
            holdInkReplacement: true,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenTargetInkIsUnresolvedButHoldRequested()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            hasTargetInkStrokes: false,
            holdInkReplacement: true,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void ShouldClearWhenUnresolved_ShouldReturnTrue_WhenDecisionRequiresClearAndInkIsUnresolved()
    {
        var decision = new CrossPageNeighborInkFrameDecision(
            ClearCurrentFrame: true,
            AllowResolvedInkReplacement: false,
            KeepVisible: false);

        CrossPageNeighborInkFramePolicy.ShouldClearWhenUnresolved(
            decision,
            hasResolvedInkBitmap: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldClearWhenUnresolved_ShouldReturnFalse_WhenResolvedInkBitmapExists()
    {
        var decision = new CrossPageNeighborInkFrameDecision(
            ClearCurrentFrame: true,
            AllowResolvedInkReplacement: true,
            KeepVisible: true);

        CrossPageNeighborInkFramePolicy.ShouldClearWhenUnresolved(
            decision,
            hasResolvedInkBitmap: true).Should().BeFalse();
    }
}
