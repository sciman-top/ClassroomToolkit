using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayInputRoutingPolicyTests
{
    [Theory]
    [InlineData(true, false, true, true, (int)OverlayWheelInputRoute.ConsumeForBoard)]
    [InlineData(false, true, true, true, (int)OverlayWheelInputRoute.HandlePhoto)]
    [InlineData(false, false, false, true, (int)OverlayWheelInputRoute.Ignore)]
    [InlineData(false, false, true, false, (int)OverlayWheelInputRoute.Ignore)]
    [InlineData(false, false, true, true, (int)OverlayWheelInputRoute.RoutePresentation)]
    public void ResolveWheelRoute_ShouldMatchExpected(
        bool boardActive,
        bool photoModeActive,
        bool canRoutePresentationInput,
        bool presentationChannelEnabled,
        int expectedValue)
    {
        var expected = (OverlayWheelInputRoute)expectedValue;
        var route = OverlayInputRoutingPolicy.ResolveWheelRoute(
            boardActive,
            photoModeActive,
            canRoutePresentationInput,
            presentationChannelEnabled);

        route.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, false, true, (int)OverlayKeyInputRoute.Consume)]
    [InlineData(false, true, false, true, (int)OverlayKeyInputRoute.Consume)]
    [InlineData(false, false, true, true, (int)OverlayKeyInputRoute.Ignore)]
    [InlineData(false, false, false, false, (int)OverlayKeyInputRoute.Ignore)]
    [InlineData(false, false, false, true, (int)OverlayKeyInputRoute.RoutePresentation)]
    public void ResolveKeyRoute_ShouldMatchExpected(
        bool photoLoading,
        bool photoKeyHandled,
        bool photoOrBoardActive,
        bool canRoutePresentationInput,
        int expectedValue)
    {
        var expected = (OverlayKeyInputRoute)expectedValue;
        var route = OverlayInputRoutingPolicy.ResolveKeyRoute(
            photoLoading,
            photoKeyHandled,
            photoOrBoardActive,
            canRoutePresentationInput);

        route.Should().Be(expected);
    }
}
