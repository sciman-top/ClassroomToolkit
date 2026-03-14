using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveInkReplacementPolicyTests
{
    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenResolvedInkMissing()
    {
        CrossPageInteractiveInkReplacementPolicy.ShouldReplace(
            hasResolvedInkBitmap: false,
            holdInkReplacement: false,
            hasCurrentInkFrame: false,
            slotPageChanged: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenHoldActiveAndCurrentFrameExistsAndSlotChanged()
    {
        CrossPageInteractiveInkReplacementPolicy.ShouldReplace(
            hasResolvedInkBitmap: true,
            holdInkReplacement: true,
            hasCurrentInkFrame: true,
            slotPageChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenSlotChangedAndCurrentFrameExists()
    {
        CrossPageInteractiveInkReplacementPolicy.ShouldReplace(
            hasResolvedInkBitmap: true,
            holdInkReplacement: false,
            hasCurrentInkFrame: true,
            slotPageChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenSlotChangedAndHoldActive()
    {
        CrossPageInteractiveInkReplacementPolicy.ShouldReplace(
            hasResolvedInkBitmap: true,
            holdInkReplacement: true,
            hasCurrentInkFrame: true,
            slotPageChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenNoCurrentFrame()
    {
        CrossPageInteractiveInkReplacementPolicy.ShouldReplace(
            hasResolvedInkBitmap: true,
            holdInkReplacement: true,
            hasCurrentInkFrame: false,
            slotPageChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenHoldActiveButSlotUnchanged()
    {
        CrossPageInteractiveInkReplacementPolicy.ShouldReplace(
            hasResolvedInkBitmap: true,
            holdInkReplacement: true,
            hasCurrentInkFrame: true,
            slotPageChanged: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenHoldInactiveAndSlotUnchanged()
    {
        CrossPageInteractiveInkReplacementPolicy.ShouldReplace(
            hasResolvedInkBitmap: true,
            holdInkReplacement: false,
            hasCurrentInkFrame: true,
            slotPageChanged: false).Should().BeTrue();
    }
}
