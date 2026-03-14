using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayNavigationFocusSnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldComposeOverlayAndUtilityState()
    {
        var utility = new FloatingUtilityActivitySnapshot(
            ToolbarActive: false,
            RollCallActive: true,
            ImageManagerActive: false,
            LauncherActive: true);

        var snapshot = OverlayNavigationFocusSnapshotPolicy.Resolve(
            overlayVisible: true,
            overlayActive: false,
            utilityActivity: utility);

        snapshot.OverlayVisible.Should().BeTrue();
        snapshot.OverlayActive.Should().BeFalse();
        snapshot.UtilityActivity.Should().Be(utility);
    }
}
