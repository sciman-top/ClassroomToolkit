using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanMouseRoutingPolicyTests
{
    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnTrue_WhenPanningInPhotoCursorMode()
    {
        PhotoPanMouseRoutingPolicy.ShouldHandlePhotoPan(
                photoPanning: true,
                photoModeActive: true,
                mode: PaintToolMode.Cursor,
                inkOperationActive: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnFalse_WhenNotPanning()
    {
        PhotoPanMouseRoutingPolicy.ShouldHandlePhotoPan(
                photoPanning: false,
                photoModeActive: true,
                mode: PaintToolMode.Cursor,
                inkOperationActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnFalse_WhenNotPhotoMode()
    {
        PhotoPanMouseRoutingPolicy.ShouldHandlePhotoPan(
                photoPanning: true,
                photoModeActive: false,
                mode: PaintToolMode.Cursor,
                inkOperationActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnFalse_WhenNotCursorMode()
    {
        PhotoPanMouseRoutingPolicy.ShouldHandlePhotoPan(
                photoPanning: true,
                photoModeActive: true,
                mode: PaintToolMode.Brush,
                inkOperationActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnFalse_WhenInkOperationActive()
    {
        PhotoPanMouseRoutingPolicy.ShouldHandlePhotoPan(
                photoPanning: true,
                photoModeActive: true,
                mode: PaintToolMode.Cursor,
                inkOperationActive: true)
            .Should()
            .BeFalse();
    }
}
