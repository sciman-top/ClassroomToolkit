using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayTopmostEnforcePolicyTests
{
    [Fact]
    public void ResolveForPhotoFullscreen_ShouldReturnFalse_WhenOverlayAlreadyTopmost()
    {
        OverlayTopmostEnforcePolicy.ResolveForPhotoFullscreen(overlayCurrentlyTopmost: true)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ResolveForPhotoFullscreen_ShouldReturnTrue_WhenOverlayNotTopmost()
    {
        OverlayTopmostEnforcePolicy.ResolveForPhotoFullscreen(overlayCurrentlyTopmost: false)
            .Should()
            .BeTrue();
    }
}
