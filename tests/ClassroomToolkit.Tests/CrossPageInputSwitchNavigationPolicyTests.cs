using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchNavigationPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush, true, false, false)]
    [InlineData((int)PaintToolMode.Eraser, false, true, false)]
    [InlineData((int)PaintToolMode.RegionErase, false, false, true)]
    public void Resolve_ShouldReturnStablePath_WhenMutatingInkAcrossPages(
        int modeValue,
        bool strokeInProgress,
        bool isErasing,
        bool isRegionSelecting)
    {
        var plan = CrossPageInputSwitchNavigationPolicy.Resolve(
            (PaintToolMode)modeValue,
            strokeInProgress,
            isErasing,
            isRegionSelecting);

        plan.InteractiveSwitch.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }

    [Theory]
    [InlineData((int)PaintToolMode.Brush, false, false, false)]
    [InlineData((int)PaintToolMode.Eraser, false, false, false)]
    [InlineData((int)PaintToolMode.Cursor, false, false, false)]
    public void Resolve_ShouldKeepInteractivePath_WhenNoActiveInkMutation(
        int modeValue,
        bool strokeInProgress,
        bool isErasing,
        bool isRegionSelecting)
    {
        var plan = CrossPageInputSwitchNavigationPolicy.Resolve(
            (PaintToolMode)modeValue,
            strokeInProgress,
            isErasing,
            isRegionSelecting);

        plan.InteractiveSwitch.Should().BeTrue();
        plan.DeferCrossPageDisplayUpdate.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnStablePath_WhenSwitchWasTriggeredByActiveBrushMutation()
    {
        var plan = CrossPageInputSwitchNavigationPolicy.Resolve(
            PaintToolMode.Brush,
            strokeInProgress: false,
            isErasing: false,
            isRegionSelecting: false,
            inputTriggeredByActiveInkMutation: true);

        plan.InteractiveSwitch.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }
}
