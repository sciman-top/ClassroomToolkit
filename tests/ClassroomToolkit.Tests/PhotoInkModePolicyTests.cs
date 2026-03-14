using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoInkModePolicyTests
{
    [Fact]
    public void IsActive_ShouldReturnTrue_WhenPhotoModeOn_AndBoardOff()
    {
        PhotoInkModePolicy.IsActive(
            photoModeActive: true,
            boardActive: false).Should().BeTrue();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenPhotoModeOff()
    {
        PhotoInkModePolicy.IsActive(
            photoModeActive: false,
            boardActive: false).Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenBoardOn()
    {
        PhotoInkModePolicy.IsActive(
            photoModeActive: true,
            boardActive: true).Should().BeFalse();
    }
}
