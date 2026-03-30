using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayHitTestPolicyTests
{
    [Theory]
    [InlineData(PaintToolMode.Cursor, false, false, false)]
    [InlineData(PaintToolMode.Cursor, true, false, true)]
    [InlineData(PaintToolMode.Brush, false, false, true)]
    [InlineData(PaintToolMode.Brush, true, false, true)]
    [InlineData(PaintToolMode.Cursor, true, true, false)]
    [InlineData(PaintToolMode.Brush, true, true, false)]
    public void ShouldEnableOverlayHitTest_ShouldMatchExpected(
        PaintToolMode mode,
        bool photoModeActive,
        bool photoLoading,
        bool expected)
    {
        var enabled = OverlayHitTestPolicy.ShouldEnableOverlayHitTest(
            mode,
            photoModeActive,
            photoLoading);

        enabled.Should().Be(expected);
    }
}
