using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputSwitchExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldSkip_WhenTargetPageInvalid()
    {
        var plan = CrossPageInputSwitchExecutionPolicy.Resolve(
            currentPage: 3,
            targetPage: 3,
            mode: PaintToolMode.Brush,
            currentPageHeight: 800);

        plan.ShouldSwitch.Should().BeFalse();
        plan.ShouldResolveBrushContinuation.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldEnableBrushContinuation_WhenBrushAndPageHeightPositive()
    {
        var plan = CrossPageInputSwitchExecutionPolicy.Resolve(
            currentPage: 3,
            targetPage: 4,
            mode: PaintToolMode.Brush,
            currentPageHeight: 800);

        plan.ShouldSwitch.Should().BeTrue();
        plan.ShouldResolveBrushContinuation.Should().BeTrue();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableBrushContinuation_WhenBrushButPageHeightNonPositive()
    {
        var plan = CrossPageInputSwitchExecutionPolicy.Resolve(
            currentPage: 3,
            targetPage: 4,
            mode: PaintToolMode.Brush,
            currentPageHeight: 0);

        plan.ShouldSwitch.Should().BeTrue();
        plan.ShouldResolveBrushContinuation.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDeferDisplayUpdate_WhenNotBrushMode()
    {
        var plan = CrossPageInputSwitchExecutionPolicy.Resolve(
            currentPage: 3,
            targetPage: 2,
            mode: PaintToolMode.Eraser,
            currentPageHeight: 600);

        plan.ShouldSwitch.Should().BeTrue();
        plan.ShouldResolveBrushContinuation.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeTrue();
    }
}
