using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldReturnInteractiveInterval_WhenPhotoModeActive()
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

        var value = ToolbarInteractionRetouchIntervalPolicy.ResolveMs(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        value.Should().Be(220);
    }

    [Fact]
    public void ResolveMs_ShouldReturnInteractiveInterval_WhenWhiteboardActive()
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

        var value = ToolbarInteractionRetouchIntervalPolicy.ResolveMs(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        value.Should().Be(220);
    }

    [Fact]
    public void ResolveMs_ShouldReturnDefault_WhenSceneNotInteractive()
    {
        var snapshot = new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: false,
            PhotoModeActive: true,
            WhiteboardActive: true,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: true);

        var value = ToolbarInteractionRetouchIntervalPolicy.ResolveMs(
            snapshot,
            ToolbarInteractionRetouchTrigger.Activated);

        value.Should().Be(120);
    }
}
