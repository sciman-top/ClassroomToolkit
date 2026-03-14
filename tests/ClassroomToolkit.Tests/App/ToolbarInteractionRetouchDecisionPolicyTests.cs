using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchDecisionPolicyTests
{
    [Fact]
    public void Resolve_ShouldDisableRetouch_WhenOverlayIsHidden()
    {
        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: false,
                PhotoModeActive: true,
                WhiteboardActive: true,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: true),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.SceneNotInteractive);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Resolve_ShouldEnableRetouchWithoutForce_WhenInteractiveSceneIsActive(
        bool photoModeActive,
        bool whiteboardActive)
    {
        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: photoModeActive,
                WhiteboardActive: whiteboardActive,
                ToolbarVisible: true,
                ToolbarTopmost: false,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: true),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.None);
    }

    [Fact]
    public void Resolve_ShouldNotForce_WhenNoInteractiveSceneIsActive()
    {
        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: false,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: false,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.SceneNotInteractive);
    }

    [Fact]
    public void Resolve_ShouldForceRetouch_OnPreviewMouseDown_WhenLauncherVisibleInInteractiveScene()
    {
        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: true,
                ToolbarVisible: true,
                ToolbarTopmost: false,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.PreviewMouseDown);

        decision.ShouldRetouch.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.None);
    }

    [Fact]
    public void Resolve_ShouldSkipRetouch_OnPreviewMouseDown_WhenLauncherNotVisible()
    {
        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: true,
                RollCallTopmost: true,
                LauncherVisible: false,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.PreviewMouseDown);

        decision.ShouldRetouch.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.PreviewMouseDown);
    }

    [Fact]
    public void Resolve_ShouldForceRetouch_WhenNoTopmostDriftButLauncherVisible()
    {
        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: true,
                RollCallTopmost: true,
                LauncherVisible: true,
                LauncherTopmost: true),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.None);
    }

    [Fact]
    public void Resolve_ShouldSkipRetouch_WhenNoTopmostDriftAndLauncherNotVisible()
    {
        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: true,
                RollCallTopmost: true,
                LauncherVisible: false,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.NoTopmostDrift);
    }

    [Fact]
    public void Resolve_ShouldRetouchWithoutForce_WhenLauncherLosesTopmost()
    {
        var decision = ToolbarInteractionRetouchDecisionPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: false),
            ToolbarInteractionRetouchTrigger.Activated);

        decision.ShouldRetouch.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(ToolbarInteractionRetouchDecisionReason.None);
    }
}
