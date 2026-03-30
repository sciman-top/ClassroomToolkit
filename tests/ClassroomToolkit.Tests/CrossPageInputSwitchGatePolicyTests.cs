using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchGatePolicyTests
{
    [Fact]
    public void CanSwitchForInput_ShouldReturnFalse_WhenNotPhotoOrNotCrossPage()
    {
        CrossPageInputSwitchGatePolicy.CanSwitchForInput(
                photoModeActive: false,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeFalse();

        CrossPageInputSwitchGatePolicy.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: false,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CanSwitchForInput_ShouldReturnFalse_WhenToolUnsupported()
    {
        CrossPageInputSwitchGatePolicy.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Cursor,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CanSwitchForInput_ShouldReturnFalse_WhenPanningOrDragging()
    {
        CrossPageInputSwitchGatePolicy.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: true,
                crossPageDragging: false)
            .Should()
            .BeFalse();

        CrossPageInputSwitchGatePolicy.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: true)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CanSwitchForInput_ShouldReturnTrue_WhenBrushOrEraserAndIdle()
    {
        CrossPageInputSwitchGatePolicy.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeTrue();

        CrossPageInputSwitchGatePolicy.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Eraser,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void CanSwitchForInput_ShouldReturnFalse_WhenWhiteboardActive()
    {
        CrossPageInputSwitchGatePolicy.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: true,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeFalse();
    }
}
