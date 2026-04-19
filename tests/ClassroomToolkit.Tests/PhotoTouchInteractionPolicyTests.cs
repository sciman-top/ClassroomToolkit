using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTouchInteractionPolicyTests
{
    [Theory]
    [InlineData(true, false, PaintToolMode.Cursor, false, 1, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, 2, false)]
    [InlineData(true, false, PaintToolMode.Brush, false, 1, false)]
    [InlineData(true, false, PaintToolMode.Cursor, true, 1, false)]
    [InlineData(false, false, PaintToolMode.Cursor, false, 1, false)]
    public void ShouldUseSingleTouchPan_ShouldMatchExpected(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        int activeTouchCount,
        bool expected)
    {
        PhotoTouchInteractionPolicy.ShouldUseSingleTouchPan(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            activeTouchCount).Should().Be(expected);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void ShouldUseManipulationZoom_ShouldRequireTwoTouches(
        int activeTouchCount,
        bool expected)
    {
        PhotoTouchInteractionPolicy.ShouldUseManipulationZoom(activeTouchCount).Should().Be(expected);
    }

    [Theory]
    [InlineData(TabletDeviceType.Touch, true)]
    [InlineData(TabletDeviceType.Stylus, false)]
    public void ShouldIgnorePromotedTouchStylus_ShouldMatchExpected(
        TabletDeviceType tabletDeviceType,
        bool expected)
    {
        PhotoTouchInteractionPolicy.ShouldIgnorePromotedTouchStylus(tabletDeviceType).Should().Be(expected);
    }
}
