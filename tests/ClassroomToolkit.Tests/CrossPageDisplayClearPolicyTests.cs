using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayClearPolicyTests
{
    [Fact]
    public void ShouldClearNeighborPages_ShouldReturnTrue_WhenTotalPagesNotGreaterThanOne()
    {
        CrossPageDisplayClearPolicy.ShouldClearNeighborPages(
            totalPages: 1,
            hasCurrentBitmap: true,
            currentPageHeight: 100).Should().BeTrue();
    }

    [Fact]
    public void ShouldClearNeighborPages_ShouldReturnTrue_WhenCurrentBitmapMissing()
    {
        CrossPageDisplayClearPolicy.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: false,
            currentPageHeight: 100).Should().BeTrue();
    }

    [Fact]
    public void ShouldClearNeighborPages_ShouldReturnTrue_WhenCurrentPageHeightInvalid()
    {
        CrossPageDisplayClearPolicy.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: true,
            currentPageHeight: 0).Should().BeTrue();
    }

    [Fact]
    public void ShouldClearNeighborPages_ShouldReturnFalse_WhenInputsAreValid()
    {
        CrossPageDisplayClearPolicy.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: true,
            currentPageHeight: 320).Should().BeFalse();
    }
}
