using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingWindowRuntimeSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldTreatMinimizedImageManagerAsNotVisible()
    {
        var snapshot = FloatingWindowRuntimeSnapshotPolicy.Resolve(
            overlayVisible: true,
            overlayActive: false,
            photoActive: true,
            presentationFullscreen: false,
            whiteboardActive: false,
            imageManagerVisible: true,
            imageManagerMinimized: true,
            launcherVisible: true);

        snapshot.ImageManagerVisible.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldKeepImageManagerVisibleWhenNotMinimized()
    {
        var snapshot = FloatingWindowRuntimeSnapshotPolicy.Resolve(
            overlayVisible: true,
            overlayActive: false,
            photoActive: false,
            presentationFullscreen: true,
            whiteboardActive: false,
            imageManagerVisible: true,
            imageManagerMinimized: false,
            launcherVisible: false);

        snapshot.ImageManagerVisible.Should().BeTrue();
        snapshot.PresentationFullscreen.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldPreserveOverlayAndLauncherFlags()
    {
        var snapshot = FloatingWindowRuntimeSnapshotPolicy.Resolve(
            overlayVisible: true,
            overlayActive: true,
            photoActive: false,
            presentationFullscreen: false,
            whiteboardActive: true,
            imageManagerVisible: false,
            imageManagerMinimized: false,
            launcherVisible: true);

        snapshot.OverlayVisible.Should().BeTrue();
        snapshot.OverlayActive.Should().BeTrue();
        snapshot.WhiteboardActive.Should().BeTrue();
        snapshot.LauncherVisible.Should().BeTrue();
    }
}
