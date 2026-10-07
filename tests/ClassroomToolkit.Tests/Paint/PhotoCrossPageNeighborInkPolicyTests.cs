using ClassroomToolkit.App.Paint;
using AwesomeAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class PhotoCrossPageNeighborInkPolicyTests
{
    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldBeFalse_WhenSlotPageChangedDuringDrag()
    {
        var keep = CrossPageNeighborInkPolicies.ShouldKeepExistingInkFrame(
            slotPageChanged: true,
            hasExistingInkFrame: true);

        keep.Should().BeFalse();
    }

    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldBeTrue_WhenSlotUnchangedAndDragging()
    {
        var keep = CrossPageNeighborInkPolicies.ShouldKeepExistingInkFrame(
            slotPageChanged: false,
            hasExistingInkFrame: true);

        keep.Should().BeTrue();
    }

    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldBeTrue_WhenSlotUnchangedAndIdle()
    {
        var keep = CrossPageNeighborInkPolicies.ShouldKeepExistingInkFrame(
            slotPageChanged: false,
            hasExistingInkFrame: true);

        keep.Should().BeTrue();
    }

    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldBeFalse_WhenNoExistingFrame()
    {
        var keep = CrossPageNeighborInkPolicies.ShouldKeepExistingInkFrame(
            slotPageChanged: false,
            hasExistingInkFrame: false);

        keep.Should().BeFalse();
    }
}
