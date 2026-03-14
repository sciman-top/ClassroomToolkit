using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageRegionErasePolicyTests
{
    [Fact]
    public void ShouldUseCrossPageErase_ShouldReturnTrue_WhenPhotoInkAndCrossPageEnabled()
    {
        CrossPageRegionErasePolicy.ShouldUseCrossPageErase(
            photoInkModeActive: true,
            crossPageDisplayEnabled: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldUseCrossPageErase_ShouldReturnFalse_WhenPhotoInkDisabled()
    {
        CrossPageRegionErasePolicy.ShouldUseCrossPageErase(
            photoInkModeActive: false,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }

    [Fact]
    public void CanNavigateForRegionErase_ShouldReturnFalse_WhenTargetPageInvalid()
    {
        CrossPageRegionErasePolicy.CanNavigateForRegionErase(
            photoInkModeActive: true,
            crossPageDisplayEnabled: true,
            targetPage: 0).Should().BeFalse();
    }

    [Fact]
    public void CanNavigateForRegionErase_ShouldReturnTrue_WhenAllConditionsMet()
    {
        CrossPageRegionErasePolicy.CanNavigateForRegionErase(
            photoInkModeActive: true,
            crossPageDisplayEnabled: true,
            targetPage: 3).Should().BeTrue();
    }
}
