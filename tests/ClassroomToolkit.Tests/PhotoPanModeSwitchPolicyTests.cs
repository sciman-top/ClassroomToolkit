using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanModeSwitchPolicyTests
{
    [Fact]
    public void ShouldEndPan_ShouldReturnFalse_WhenPanNotActive()
    {
        PhotoPanModeSwitchPolicy.ShouldEndPan(
            photoPanning: false,
            photoModeActive: true,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inkOperationActive: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnFalse_WhenPanStillAllowed()
    {
        PhotoPanModeSwitchPolicy.ShouldEndPan(
            photoPanning: true,
            photoModeActive: true,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inkOperationActive: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnTrue_WhenSwitchToDrawMode()
    {
        PhotoPanModeSwitchPolicy.ShouldEndPan(
            photoPanning: true,
            photoModeActive: true,
            boardActive: false,
            mode: PaintToolMode.Brush,
            inkOperationActive: false).Should().BeTrue();
    }
}
