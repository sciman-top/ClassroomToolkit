using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class OverlayPresentationCommandRouterTests
{
    [Fact]
    public void TrySend_ShouldThrowArgumentNullException_WhenWpsSenderIsNull()
    {
        var context = new OverlayPresentationCommandRouteContext(
            ForegroundType: OverlayPresentationRouteType.Wps,
            CurrentPresentationType: OverlayPresentationRouteType.None,
            WpsSlideshow: true,
            OfficeSlideshow: false,
            WpsFullscreen: false,
            OfficeFullscreen: false);

        var act = () => OverlayPresentationCommandRouter.TrySend(
            context,
            trySendWps: null!,
            trySendOffice: _ => false);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrySend_ShouldThrowArgumentNullException_WhenOfficeSenderIsNull()
    {
        var context = new OverlayPresentationCommandRouteContext(
            ForegroundType: OverlayPresentationRouteType.Office,
            CurrentPresentationType: OverlayPresentationRouteType.None,
            WpsSlideshow: false,
            OfficeSlideshow: true,
            WpsFullscreen: false,
            OfficeFullscreen: false);

        var act = () => OverlayPresentationCommandRouter.TrySend(
            context,
            trySendWps: _ => false,
            trySendOffice: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TrySend_ShouldPreferForegroundWps()
    {
        var context = new OverlayPresentationCommandRouteContext(
            ForegroundType: OverlayPresentationRouteType.Wps,
            CurrentPresentationType: OverlayPresentationRouteType.None,
            WpsSlideshow: true,
            OfficeSlideshow: true,
            WpsFullscreen: false,
            OfficeFullscreen: false);
        var wpsCalls = new List<bool>();
        var officeCalls = new List<bool>();

        var result = OverlayPresentationCommandRouter.TrySend(
            context,
            allowBackground =>
            {
                wpsCalls.Add(allowBackground);
                return true;
            },
            allowBackground =>
            {
                officeCalls.Add(allowBackground);
                return true;
            });

        result.Should().BeTrue();
        wpsCalls.Should().Equal(false);
        officeCalls.Should().BeEmpty();
    }

    [Fact]
    public void TrySend_ShouldPreferForegroundOffice()
    {
        var context = new OverlayPresentationCommandRouteContext(
            ForegroundType: OverlayPresentationRouteType.Office,
            CurrentPresentationType: OverlayPresentationRouteType.None,
            WpsSlideshow: true,
            OfficeSlideshow: true,
            WpsFullscreen: false,
            OfficeFullscreen: false);
        var wpsCalls = new List<bool>();
        var officeCalls = new List<bool>();

        var result = OverlayPresentationCommandRouter.TrySend(
            context,
            allowBackground =>
            {
                wpsCalls.Add(allowBackground);
                return true;
            },
            allowBackground =>
            {
                officeCalls.Add(allowBackground);
                return true;
            });

        result.Should().BeTrue();
        officeCalls.Should().Equal(false);
        wpsCalls.Should().BeEmpty();
    }

    [Fact]
    public void TrySend_ShouldPreferCurrentType_WhenBothSlideshowsAvailable()
    {
        var context = new OverlayPresentationCommandRouteContext(
            ForegroundType: OverlayPresentationRouteType.None,
            CurrentPresentationType: OverlayPresentationRouteType.Wps,
            WpsSlideshow: true,
            OfficeSlideshow: true,
            WpsFullscreen: false,
            OfficeFullscreen: false);
        var wpsCalls = new List<bool>();
        var officeCalls = new List<bool>();

        var result = OverlayPresentationCommandRouter.TrySend(
            context,
            allowBackground =>
            {
                wpsCalls.Add(allowBackground);
                return true;
            },
            allowBackground =>
            {
                officeCalls.Add(allowBackground);
                return true;
            });

        result.Should().BeTrue();
        wpsCalls.Should().Equal(true);
        officeCalls.Should().BeEmpty();
    }

    [Fact]
    public void TrySend_ShouldPreferFullscreenTarget_WhenCurrentTypeIsNotAvailable()
    {
        var context = new OverlayPresentationCommandRouteContext(
            ForegroundType: OverlayPresentationRouteType.None,
            CurrentPresentationType: OverlayPresentationRouteType.None,
            WpsSlideshow: true,
            OfficeSlideshow: true,
            WpsFullscreen: false,
            OfficeFullscreen: true);
        var wpsCalls = new List<bool>();
        var officeCalls = new List<bool>();

        var result = OverlayPresentationCommandRouter.TrySend(
            context,
            allowBackground =>
            {
                wpsCalls.Add(allowBackground);
                return false;
            },
            allowBackground =>
            {
                officeCalls.Add(allowBackground);
                return true;
            });

        result.Should().BeTrue();
        officeCalls.Should().Equal(true);
        wpsCalls.Should().BeEmpty();
    }

    [Fact]
    public void TrySend_ShouldReturnFalse_WhenNoRouteCanSend()
    {
        var context = new OverlayPresentationCommandRouteContext(
            ForegroundType: OverlayPresentationRouteType.None,
            CurrentPresentationType: OverlayPresentationRouteType.None,
            WpsSlideshow: false,
            OfficeSlideshow: false,
            WpsFullscreen: false,
            OfficeFullscreen: false);
        var wpsCalls = new List<bool>();
        var officeCalls = new List<bool>();

        var result = OverlayPresentationCommandRouter.TrySend(
            context,
            allowBackground =>
            {
                wpsCalls.Add(allowBackground);
                return false;
            },
            allowBackground =>
            {
                officeCalls.Add(allowBackground);
                return false;
            });

        result.Should().BeFalse();
        wpsCalls.Should().BeEmpty();
        officeCalls.Should().BeEmpty();
    }

    [Fact]
    public void TrySend_ShouldReturnFalseWithoutThrow_WhenWpsSenderThrowsNonFatal()
    {
        var context = new OverlayPresentationCommandRouteContext(
            ForegroundType: OverlayPresentationRouteType.Wps,
            CurrentPresentationType: OverlayPresentationRouteType.None,
            WpsSlideshow: true,
            OfficeSlideshow: false,
            WpsFullscreen: false,
            OfficeFullscreen: false);

        var result = OverlayPresentationCommandRouter.TrySend(
            context,
            _ => throw new InvalidOperationException("wps-send-failed"),
            _ => false);

        result.Should().BeFalse();
    }
}
