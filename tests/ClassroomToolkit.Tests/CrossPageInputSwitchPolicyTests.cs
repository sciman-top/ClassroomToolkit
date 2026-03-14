using System.Windows;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchPolicyTests
{
    [Fact]
    public void ShouldSwitchByPointer_ShouldReturnFalse_WhenPointerInsideExpandedRect()
    {
        var rect = new Rect(100, 100, 300, 400);
        var pointer = new Point(95, 120); // outside raw rect, inside +hysteresis

        var shouldSwitch = CrossPageInputSwitchPolicy.ShouldSwitchByPointer(
            rect,
            pointer,
            hysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeFalse();
    }

    [Fact]
    public void ShouldSwitchByPointer_ShouldReturnTrue_WhenPointerOutsideExpandedRect()
    {
        var rect = new Rect(100, 100, 300, 400);
        var pointer = new Point(80, 120); // outside +hysteresis

        var shouldSwitch = CrossPageInputSwitchPolicy.ShouldSwitchByPointer(
            rect,
            pointer,
            hysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeTrue();
    }
}
