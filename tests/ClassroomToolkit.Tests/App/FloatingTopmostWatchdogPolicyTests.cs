using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostWatchdogPolicyTests
{
    [Fact]
    public void ShouldForceRetouch_ShouldReturnFalse_WhenPhotoModeActive()
    {
        var shouldRetouch = FloatingTopmostWatchdogPolicy.ShouldForceRetouch(
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: false,
            imageManagerVisible: false,
            rollCallAuxOverlayVisible: false,
            photoModeActive: true);

        shouldRetouch.Should().BeFalse();
    }

    [Fact]
    public void ShouldForceRetouch_ShouldReturnTrue_WhenPhotoModeInactiveAndAnyUtilityVisible()
    {
        var shouldRetouch = FloatingTopmostWatchdogPolicy.ShouldForceRetouch(
            toolbarVisible: false,
            rollCallVisible: true,
            launcherVisible: false,
            imageManagerVisible: false,
            rollCallAuxOverlayVisible: false,
            photoModeActive: false);

        shouldRetouch.Should().BeTrue();
    }
}
