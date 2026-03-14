using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class PhotoOverlayReentryPolicyTests
{
    [Fact]
    public void Resolve_ShouldNormalizeAndActivate_WhenReenteringSamePhotoFromMinimizedState()
    {
        var plan = PhotoOverlayReentryPolicy.Resolve(
            windowMinimized: true,
            photoModeActive: true,
            sameSourcePath: true);

        plan.NormalizeWindowState.Should().BeTrue();
        plan.ActivateOverlay.Should().BeTrue();
        plan.ReturnEarly.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotReturnEarly_WhenPathChanges()
    {
        var plan = PhotoOverlayReentryPolicy.Resolve(
            windowMinimized: false,
            photoModeActive: true,
            sameSourcePath: false);

        plan.NormalizeWindowState.Should().BeFalse();
        plan.ActivateOverlay.Should().BeFalse();
        plan.ReturnEarly.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldOnlyNormalize_WhenEnteringNewPhotoFromMinimizedState()
    {
        var plan = PhotoOverlayReentryPolicy.Resolve(
            windowMinimized: true,
            photoModeActive: false,
            sameSourcePath: false);

        plan.NormalizeWindowState.Should().BeTrue();
        plan.ActivateOverlay.Should().BeFalse();
        plan.ReturnEarly.Should().BeFalse();
    }
}
