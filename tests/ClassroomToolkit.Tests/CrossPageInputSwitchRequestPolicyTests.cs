using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchRequestPolicyTests
{
    [Fact]
    public void ShouldSwitchForInput_ShouldReturnFalse_WhenGateRejects()
    {
        var shouldSwitch = CrossPageInputSwitchRequestPolicy.ShouldSwitchForInput(
            photoModeActive: false,
            crossPageDisplayEnabled: true,
            boardActive: false,
            mode: PaintToolMode.Brush,
            photoPanning: false,
            crossPageDragging: false,
            hasBitmap: true,
            currentPageRect: new Rect(100, 100, 200, 200),
            pointer: new Point(20, 20),
            pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeFalse();
    }

    [Fact]
    public void ShouldSwitchForInput_ShouldReturnTrue_WhenNoCurrentRectButGateAndBitmapPass()
    {
        var shouldSwitch = CrossPageInputSwitchRequestPolicy.ShouldSwitchForInput(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            boardActive: false,
            mode: PaintToolMode.Brush,
            photoPanning: false,
            crossPageDragging: false,
            hasBitmap: true,
            currentPageRect: null,
            pointer: new Point(20, 20),
            pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeTrue();
    }

    [Fact]
    public void ShouldSwitchForInput_ShouldReturnFalse_WhenPointerInsideHysteresisRange()
    {
        var shouldSwitch = CrossPageInputSwitchRequestPolicy.ShouldSwitchForInput(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            boardActive: false,
            mode: PaintToolMode.Brush,
            photoPanning: false,
            crossPageDragging: false,
            hasBitmap: true,
            currentPageRect: new Rect(100, 100, 300, 300),
            pointer: new Point(95, 120),
            pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeFalse();
    }

    [Fact]
    public void ShouldSwitchForInput_ShouldReturnTrue_WhenPointerOutsideHysteresisRange()
    {
        var shouldSwitch = CrossPageInputSwitchRequestPolicy.ShouldSwitchForInput(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            boardActive: false,
            mode: PaintToolMode.Eraser,
            photoPanning: false,
            crossPageDragging: false,
            hasBitmap: true,
            currentPageRect: new Rect(100, 100, 300, 300),
            pointer: new Point(20, 20),
            pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeTrue();
    }
}
