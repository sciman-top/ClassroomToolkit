using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostDriftPolicyTests
{
    [Fact]
    public void ResolveDrift_ShouldReturnLauncherDrift_WhenLauncherVisibleButNotTopmost()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: false);

        var decision = FloatingTopmostDriftPolicy.ResolveDrift(snapshot);
        decision.HasDrift.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostDriftReason.LauncherDrift);
    }

    [Fact]
    public void ResolveDrift_ShouldReturnNoDrift_WhenAllVisibleWindowsAreTopmost()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: true,
            RollCallTopmost: true,
            LauncherVisible: true,
            LauncherTopmost: true);

        var decision = FloatingTopmostDriftPolicy.ResolveDrift(snapshot);
        decision.HasDrift.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostDriftReason.NoDrift);
    }

    [Fact]
    public void ResolveForceEnforce_ShouldReturnDisabledByDesign_WhenLauncherNotTopmost()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: false);

        var decision = FloatingTopmostDriftPolicy.ResolveForceEnforce(snapshot);
        decision.ShouldForceEnforce.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostForceEnforceReason.DisabledByDesign);
    }

    [Fact]
    public void ResolveForceEnforce_ShouldReturnDisabledByDesign_WhenLauncherHidden()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: false,
            LauncherTopmost: false);

        var decision = FloatingTopmostDriftPolicy.ResolveForceEnforce(snapshot);
        decision.ShouldForceEnforce.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostForceEnforceReason.DisabledByDesign);
    }

    [Fact]
    public void HasDrift_ShouldMapResolveDecision()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: true);

        FloatingTopmostDriftPolicy.HasDrift(snapshot).Should().BeTrue();
    }
}
