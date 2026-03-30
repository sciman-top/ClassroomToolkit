using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingWindowActivationSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldMapRuntimeAndTopmostIntoActivationSnapshot()
    {
        var runtimeSnapshot = new FloatingWindowRuntimeSnapshot(
            OverlayVisible: true,
            OverlayActive: false,
            PhotoActive: true,
            PresentationFullscreen: false,
            WhiteboardActive: false,
            ImageManagerVisible: true,
            LauncherVisible: true);
        var topmostPlan = new FloatingTopmostPlan(
            ToolbarTopmost: true,
            RollCallTopmost: false,
            LauncherTopmost: true,
            ImageManagerTopmost: true,
            OverlayShouldActivate: true);
        var utilityActivity = new FloatingUtilityActivitySnapshot(
            ToolbarActive: false,
            RollCallActive: true,
            ImageManagerActive: false,
            LauncherActive: false);

        var snapshot = FloatingWindowActivationSnapshotPolicy.Resolve(
            runtimeSnapshot,
            topmostPlan,
            utilityActivity);

        snapshot.OverlayVisible.Should().BeTrue();
        snapshot.OverlayShouldActivate.Should().BeTrue();
        snapshot.OverlayActive.Should().BeFalse();
        snapshot.ImageManagerTopmost.Should().BeTrue();
        snapshot.ImageManagerActive.Should().BeFalse();
        snapshot.UtilityActivity.Should().Be(utilityActivity);
    }
}
