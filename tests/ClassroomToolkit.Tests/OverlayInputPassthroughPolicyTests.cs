using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayInputPassthroughPolicyTests
{
    [Theory]
    [InlineData(PaintToolMode.Cursor, 0.0, false, true)]
    [InlineData(PaintToolMode.Cursor, 0.0005, false, true)]
    [InlineData(PaintToolMode.Cursor, 0.01, false, false)]
    [InlineData(PaintToolMode.Brush, 0.0, false, false)]
    [InlineData(PaintToolMode.Cursor, 0.0, true, false)]
    public void ShouldEnable_ShouldFollowCursorBoardPhotoGuards(
        PaintToolMode mode,
        double boardOpacity,
        bool photoModeActive,
        bool expected)
    {
        var enabled = OverlayInputPassthroughPolicy.ShouldEnable(mode, boardOpacity, photoModeActive);

        enabled.Should().Be(expected);
    }
}
