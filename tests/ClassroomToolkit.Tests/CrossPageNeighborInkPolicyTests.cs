using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborInkPolicyTests
{
    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldReturnTrue_ForSameSlotWithExistingFrame()
    {
        CrossPageNeighborInkPolicy
            .ShouldKeepExistingInkFrame(
                slotPageChanged: false,
                hasExistingInkFrame: true)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldReturnFalse_WhenSlotChanged()
    {
        CrossPageNeighborInkPolicy
            .ShouldKeepExistingInkFrame(
                slotPageChanged: true,
                hasExistingInkFrame: true)
            .Should()
            .BeFalse();
    }
}
