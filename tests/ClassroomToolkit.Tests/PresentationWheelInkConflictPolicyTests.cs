using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationWheelInkConflictPolicyTests
{
    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_InCursorMode()
    {
        var suppressed = PresentationWheelInkConflictPolicy.ShouldSuppress(
            PaintToolMode.Cursor,
            DateTime.UtcNow,
            DateTime.UtcNow,
            suppressWindowMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenNoRecentInkInput()
    {
        var suppressed = PresentationWheelInkConflictPolicy.ShouldSuppress(
            PaintToolMode.Brush,
            InkRuntimeTimingDefaults.UnsetTimestampUtc,
            DateTime.UtcNow,
            suppressWindowMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenWithinSuppressWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var suppressed = PresentationWheelInkConflictPolicy.ShouldSuppress(
            PaintToolMode.Brush,
            nowUtc.AddMilliseconds(-60),
            nowUtc,
            suppressWindowMs: 120);

        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenOutsideSuppressWindow()
    {
        var nowUtc = DateTime.UtcNow;
        var suppressed = PresentationWheelInkConflictPolicy.ShouldSuppress(
            PaintToolMode.Brush,
            nowUtc.AddMilliseconds(-220),
            nowUtc,
            suppressWindowMs: 120);

        suppressed.Should().BeFalse();
    }
}

