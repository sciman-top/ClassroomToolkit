using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingTopmostVisibilitySnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldCaptureAllVisibilityFlags()
    {
        var snapshot = FloatingTopmostVisibilitySnapshotPolicy.Resolve(
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: true,
            imageManagerVisible: false,
            overlayVisible: true);

        snapshot.ToolbarVisible.Should().BeTrue();
        snapshot.RollCallVisible.Should().BeFalse();
        snapshot.LauncherVisible.Should().BeTrue();
        snapshot.ImageManagerVisible.Should().BeFalse();
        snapshot.OverlayVisible.Should().BeTrue();
    }
}
