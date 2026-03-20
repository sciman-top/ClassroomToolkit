using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayPresentationRouteContextBuilderTests
{
    [Fact]
    public void Build_ShouldMapForegroundAndCurrentTypes()
    {
        var context = OverlayPresentationRouteContextBuilder.Build(
            foregroundType: PresentationType.Office,
            currentPresentationType: PresentationType.Wps,
            wpsSlideshow: true,
            officeSlideshow: true,
            wpsFullscreen: false,
            officeFullscreen: true);

        context.ForegroundType.Should().Be(OverlayPresentationRouteType.Office);
        context.CurrentPresentationType.Should().Be(OverlayPresentationRouteType.Wps);
    }

    [Fact]
    public void Build_ShouldDisableFullscreenPreference_WhenBothSlideshowsUnavailable()
    {
        var context = OverlayPresentationRouteContextBuilder.Build(
            foregroundType: PresentationType.None,
            currentPresentationType: PresentationType.None,
            wpsSlideshow: true,
            officeSlideshow: false,
            wpsFullscreen: true,
            officeFullscreen: true);

        context.WpsSlideshow.Should().BeTrue();
        context.OfficeSlideshow.Should().BeFalse();
        context.WpsFullscreen.Should().BeFalse();
        context.OfficeFullscreen.Should().BeFalse();
    }

    [Theory]
    [InlineData(PresentationType.None, 0)]
    [InlineData(PresentationType.Wps, 1)]
    [InlineData(PresentationType.Office, 2)]
    [InlineData(PresentationType.Other, 0)]
    public void MapRouteType_ShouldMapAsExpected(PresentationType input, int expectedValue)
    {
        var mapped = OverlayPresentationRouteContextBuilder.MapRouteType(input);

        ((int)mapped).Should().Be(expectedValue);
    }
}
