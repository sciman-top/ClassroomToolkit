using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputDisplayPolicyTests
{
    [Fact]
    public void IsActive_ShouldReturnTrue_WhenPhotoModeOnBoardOffAndCrossPageEnabled()
    {
        CrossPageInputDisplayPolicy.IsActive(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true).Should().BeTrue();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenBoardActive()
    {
        CrossPageInputDisplayPolicy.IsActive(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenCrossPageDisabled()
    {
        CrossPageInputDisplayPolicy.IsActive(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: false).Should().BeFalse();
    }
}
