using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInteractiveSwitchRefreshPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnDeferred_WhenRequested()
    {
        var mode = CrossPageInteractiveSwitchRefreshPolicy.Resolve(
            PaintToolMode.Brush,
            deferCrossPageDisplayUpdate: true);

        mode.Should().Be(CrossPageInteractiveSwitchRefreshMode.DeferredByInput);
    }

    [Fact]
    public void Resolve_ShouldReturnImmediateDirect_ForBrushWithoutDefer()
    {
        var mode = CrossPageInteractiveSwitchRefreshPolicy.Resolve(
            PaintToolMode.Brush,
            deferCrossPageDisplayUpdate: false);

        mode.Should().Be(CrossPageInteractiveSwitchRefreshMode.ImmediateDirect);
    }

    [Fact]
    public void Resolve_ShouldReturnImmediateScheduled_ForNonBrushWithoutDefer()
    {
        var mode = CrossPageInteractiveSwitchRefreshPolicy.Resolve(
            PaintToolMode.Eraser,
            deferCrossPageDisplayUpdate: false);

        mode.Should().Be(CrossPageInteractiveSwitchRefreshMode.ImmediateScheduled);
    }
}
