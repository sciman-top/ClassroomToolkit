using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoCrossPageNeighborInkPolicyTests
{
    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldBeFalse_WhenSlotPageChangedDuringDrag()
    {
        var keep = CrossPageNeighborInkPolicy.ShouldKeepExistingInkFrame(
            slotPageChanged: true,
            hasExistingInkFrame: true);

        keep.Should().BeFalse();
    }

    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldBeTrue_WhenSlotUnchangedAndDragging()
    {
        var keep = CrossPageNeighborInkPolicy.ShouldKeepExistingInkFrame(
            slotPageChanged: false,
            hasExistingInkFrame: true);

        keep.Should().BeTrue();
    }

    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldBeTrue_WhenSlotUnchangedAndIdle()
    {
        var keep = CrossPageNeighborInkPolicy.ShouldKeepExistingInkFrame(
            slotPageChanged: false,
            hasExistingInkFrame: true);

        keep.Should().BeTrue();
    }

    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldBeFalse_WhenNoExistingFrame()
    {
        var keep = CrossPageNeighborInkPolicy.ShouldKeepExistingInkFrame(
            slotPageChanged: false,
            hasExistingInkFrame: false);

        keep.Should().BeFalse();
    }
}
