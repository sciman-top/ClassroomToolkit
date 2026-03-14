using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborInkFramePolicyTests
{
    [Fact]
    public void Resolve_ShouldClear_WhenSlotChangedAndNotHeld()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            holdInkReplacement: false,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenHeld()
    {
        var decision = CrossPageNeighborInkFramePolicy.Resolve(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
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
            holdInkReplacement: false,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeTrue();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeFalse();
    }
}
