using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationFullscreenTypeResolutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnWps_WhenOnlyWpsFullscreen()
    {
        var resolved = PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen: true,
            officeFullscreen: false,
            currentPresentationType: PresentationType.None);

        resolved.Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void Resolve_ShouldReturnOffice_WhenOnlyOfficeFullscreen()
    {
        var resolved = PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen: false,
            officeFullscreen: true,
            currentPresentationType: PresentationType.None);

        resolved.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentType_WhenBothFullscreenAndCurrentKnown()
    {
        var resolved = PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.Wps,
            foregroundType: PresentationType.Other,
            foregroundIsFullscreen: true);

        resolved.Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void Resolve_ShouldPreferWps_WhenBothFullscreenAndForegroundIsWps()
    {
        var resolved = PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.Office,
            foregroundType: PresentationType.Wps,
            foregroundIsFullscreen: true);

        resolved.Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void Resolve_ShouldPreferOffice_WhenBothFullscreenAndForegroundIsOffice()
    {
        var resolved = PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.Wps,
            foregroundType: PresentationType.Office,
            foregroundIsFullscreen: true);

        resolved.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenAmbiguousAndCurrentUnknown()
    {
        var resolved = PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.None,
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false);

        resolved.Should().Be(PresentationType.None);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenCurrentTypeIsNotAChannel()
    {
        var resolved = PresentationFullscreenTypeResolutionPolicy.Resolve(
            wpsFullscreen: true,
            officeFullscreen: true,
            currentPresentationType: PresentationType.Other);

        resolved.Should().Be(PresentationType.None);
    }
}
