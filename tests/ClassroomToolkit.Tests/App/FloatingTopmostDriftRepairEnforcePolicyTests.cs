using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostDriftRepairEnforcePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_ForActivatedTrigger_WhenLauncherDriftsInInteractiveScene()
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
            LauncherTopmost: false);

        var enforce = FloatingTopmostDriftRepairEnforcePolicy.Resolve(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        enforce.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_ForPreviewMouseDownTrigger()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: false,
            WhiteboardActive: true,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: true,
            RollCallTopmost: false,
            LauncherVisible: false,
            LauncherTopmost: false);

        var enforce = FloatingTopmostDriftRepairEnforcePolicy.Resolve(
            snapshot,
            ToolbarInteractionRetouchTrigger.PreviewMouseDown);

        enforce.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_ForActivatedTrigger_WhenLauncherDoesNotDrift()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: false,
            WhiteboardActive: true,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: true);

        var enforce = FloatingTopmostDriftRepairEnforcePolicy.Resolve(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        enforce.Should().BeFalse();
    }
}
