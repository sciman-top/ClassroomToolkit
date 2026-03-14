using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingOwnerRuntimeSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldCaptureOwnerFlags()
    {
        var snapshot = FloatingOwnerRuntimeSnapshotPolicy.Resolve(
            overlayVisible: true,
            toolbarOwnerAlreadyOverlay: true,
            rollCallOwnerAlreadyOverlay: false,
            imageManagerOwnerAlreadyOverlay: true);

        snapshot.OverlayVisible.Should().BeTrue();
        snapshot.ToolbarOwnerAlreadyOverlay.Should().BeTrue();
        snapshot.RollCallOwnerAlreadyOverlay.Should().BeFalse();
        snapshot.ImageManagerOwnerAlreadyOverlay.Should().BeTrue();
    }
}
