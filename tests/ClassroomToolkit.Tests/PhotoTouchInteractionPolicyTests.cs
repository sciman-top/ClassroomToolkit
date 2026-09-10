using System.Windows.Input;
using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoTouchInteractionPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void ShouldUseManipulation_ShouldRequireAtLeastOneTouch(
        int activeTouchCount,
        bool expected)
    {
        PhotoTouchInteractionPolicy.ShouldUseManipulation(activeTouchCount).Should().Be(expected);
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
