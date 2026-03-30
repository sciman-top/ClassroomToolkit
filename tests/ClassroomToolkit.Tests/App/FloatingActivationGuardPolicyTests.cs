using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingActivationGuardPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNotBlocked_WhenNoUtilityWindowIsActive()
    {
        var decision = FloatingActivationGuardPolicy.Resolve(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: false,
                RollCallActive: false,
                ImageManagerActive: false,
                LauncherActive: false));

        decision.IsBlocked.Should().BeFalse();
        decision.Reason.Should().Be(FloatingActivationGuardReason.None);
    }

    [Theory]
    [InlineData(true, false, false, false, (int)FloatingActivationGuardReason.ToolbarActive)]
    [InlineData(false, true, false, false, (int)FloatingActivationGuardReason.RollCallActive)]
    [InlineData(false, false, true, false, (int)FloatingActivationGuardReason.ImageManagerActive)]
    [InlineData(false, false, false, true, (int)FloatingActivationGuardReason.LauncherActive)]
    public void Resolve_ShouldReturnBlockedReason_WhenAnyUtilityWindowIsActive(
        bool toolbarActive,
        bool rollCallActive,
        bool imageManagerActive,
        bool launcherActive,
        int expectedReason)
    {
        var decision = FloatingActivationGuardPolicy.Resolve(
            new FloatingUtilityActivitySnapshot(
                ToolbarActive: toolbarActive,
                RollCallActive: rollCallActive,
                ImageManagerActive: imageManagerActive,
                LauncherActive: launcherActive));

        decision.IsBlocked.Should().BeTrue();
        decision.Reason.Should().Be((FloatingActivationGuardReason)expectedReason);
    }

    [Fact]
    public void IsBlockedByUtilityWindows_ShouldMapResolveDecision()
    {
        FloatingActivationGuardPolicy.IsBlockedByUtilityWindows(
            toolbarActive: false,
            rollCallActive: false,
            imageManagerActive: true,
            launcherActive: false).Should().BeTrue();
    }
}
