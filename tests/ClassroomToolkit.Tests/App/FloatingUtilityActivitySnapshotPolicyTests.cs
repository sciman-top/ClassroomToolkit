using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public class FloatingUtilityActivitySnapshotPolicyTests
{
    [Fact]
    public void Resolve_ShouldCaptureAllUtilityFlags()
    {
        var snapshot = FloatingUtilityActivitySnapshotPolicy.Resolve(
            toolbarActive: true,
            rollCallActive: false,
            imageManagerActive: true,
            launcherActive: true);

        snapshot.ToolbarActive.Should().BeTrue();
        snapshot.RollCallActive.Should().BeFalse();
        snapshot.ImageManagerActive.Should().BeTrue();
        snapshot.LauncherActive.Should().BeTrue();
    }
}
