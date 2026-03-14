using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingWindowCoordinationSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldComposeAllSubSnapshots()
    {
        var runtime = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: true,
            OverlayActive: false,
            PhotoActive: true,
            PresentationFullscreen: false,
            WhiteboardActive: false,
            ImageManagerVisible: true,
            LauncherVisible: true);
        var launcher = new LauncherWindowRuntimeSnapshot(
            VisibleForTopmost: true,
            Active: true,
            WindowKind: LauncherWindowKind.Bubble,
            SelectionReason: LauncherWindowRuntimeSelectionReason.PreferBubbleVisible);

        var snapshot = FloatingWindowCoordinationSnapshotPolicy.Resolve(
            runtime,
            launcher,
            toolbarVisible: true,
            rollCallVisible: false,
            toolbarActive: false,
            rollCallActive: true,
            imageManagerActive: false,
            launcherActive: true,
            toolbarOwnerAlreadyOverlay: true,
            rollCallOwnerAlreadyOverlay: false,
            imageManagerOwnerAlreadyOverlay: true);

        snapshot.Runtime.Should().Be(runtime);
        snapshot.Launcher.Should().Be(launcher);
        snapshot.TopmostVisibility.ToolbarVisible.Should().BeTrue();
        snapshot.TopmostVisibility.RollCallVisible.Should().BeFalse();
        snapshot.UtilityActivity.RollCallActive.Should().BeTrue();
        snapshot.UtilityActivity.LauncherActive.Should().BeTrue();
        snapshot.Owner.ToolbarOwnerAlreadyOverlay.Should().BeTrue();
        snapshot.Owner.ImageManagerOwnerAlreadyOverlay.Should().BeTrue();
    }
}
