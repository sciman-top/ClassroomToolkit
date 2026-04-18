using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WpsFullscreenExitPolicyTests
{
    [Fact]
    public void ShouldTreatAsActiveFullscreen_ShouldReturnFalse_WhenNoFullscreenCandidate()
    {
        var active = WpsFullscreenExitPolicy.ShouldTreatAsActiveFullscreen(
            hasFullscreenCandidate: false,
            foregroundType: PresentationType.Wps,
            foregroundIsFullscreen: false,
            foregroundOwnedByCurrentProcess: false);

        active.Should().BeFalse();
    }

    [Fact]
    public void ShouldTreatAsActiveFullscreen_ShouldReturnFalse_WhenForegroundReturnedToNonFullscreenWps()
    {
        var active = WpsFullscreenExitPolicy.ShouldTreatAsActiveFullscreen(
            hasFullscreenCandidate: true,
            foregroundType: PresentationType.Wps,
            foregroundIsFullscreen: false,
            foregroundOwnedByCurrentProcess: false);

        active.Should().BeFalse();
    }

    [Fact]
    public void ShouldTreatAsActiveFullscreen_ShouldKeepFullscreen_WhenOverlayOwnsForeground()
    {
        var active = WpsFullscreenExitPolicy.ShouldTreatAsActiveFullscreen(
            hasFullscreenCandidate: true,
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false,
            foregroundOwnedByCurrentProcess: true);

        active.Should().BeTrue();
    }

    [Fact]
    public void ShouldTreatAsActiveFullscreen_ShouldKeepFullscreen_WhenUnrelatedWindowOwnsForeground()
    {
        var active = WpsFullscreenExitPolicy.ShouldTreatAsActiveFullscreen(
            hasFullscreenCandidate: true,
            foregroundType: PresentationType.Other,
            foregroundIsFullscreen: false,
            foregroundOwnedByCurrentProcess: false);

        active.Should().BeTrue();
    }
}
