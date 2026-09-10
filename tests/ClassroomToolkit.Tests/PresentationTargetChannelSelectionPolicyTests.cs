using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.Tests;

public sealed class PresentationTargetChannelSelectionPolicyTests
{
    [Fact]
    public void ResolveForFocus_ShouldPreferFullscreenForegroundChannel()
    {
        var type = PresentationTargetChannelSelectionPolicy.ResolveForFocus(
            foregroundType: PresentationType.Office,
            foregroundIsFullscreen: true,
            currentPresentationType: PresentationType.Wps,
            allowWps: true,
            allowOffice: true);

        type.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void ResolveForFocus_ShouldKeepCurrentChannel_WhenForegroundHasNoPresentationEvidence()
    {
        var type = PresentationTargetChannelSelectionPolicy.ResolveForFocus(
            foregroundType: PresentationType.Other,
            foregroundIsFullscreen: false,
            currentPresentationType: PresentationType.Wps,
            allowWps: true,
            allowOffice: true);

        type.Should().Be(PresentationType.Wps);
    }

    [Fact]
    public void ResolveForFocus_ShouldUseTheOnlyEnabledChannel()
    {
        var type = PresentationTargetChannelSelectionPolicy.ResolveForFocus(
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false,
            currentPresentationType: PresentationType.None,
            allowWps: false,
            allowOffice: true);

        type.Should().Be(PresentationType.Office);
    }

    [Fact]
    public void ResolveForFocus_ShouldFailClosed_WhenBothChannelsAreEnabledButAmbiguous()
    {
        var type = PresentationTargetChannelSelectionPolicy.ResolveForFocus(
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false,
            currentPresentationType: PresentationType.None,
            allowWps: true,
            allowOffice: true);

        type.Should().Be(PresentationType.None);
    }

    [Fact]
    public void ResolveForFocus_ShouldIgnoreCurrentChannel_WhenItIsDisabled()
    {
        var type = PresentationTargetChannelSelectionPolicy.ResolveForFocus(
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false,
            currentPresentationType: PresentationType.Wps,
            allowWps: false,
            allowOffice: true);

        type.Should().Be(PresentationType.Office);
    }
}
