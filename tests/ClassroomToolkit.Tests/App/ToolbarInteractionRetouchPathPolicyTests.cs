using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchPathPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenDecisionDisablesRetouch()
    {
        var path = ToolbarInteractionRetouchPathPolicy.Resolve(
            CreateSnapshot(),
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: false,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        path.Should().Be(ToolbarInteractionRetouchPath.None);
    }

    [Fact]
    public void Resolve_ShouldReturnZOrderApply_WhenForceEnforceRequested()
    {
        var path = ToolbarInteractionRetouchPathPolicy.Resolve(
            CreateSnapshot(),
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: true,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        path.Should().Be(ToolbarInteractionRetouchPath.ZOrderApply);
    }

    [Fact]
    public void Resolve_ShouldReturnDirectDriftRepair_WhenLauncherOnlyDrift()
    {
        var path = ToolbarInteractionRetouchPathPolicy.Resolve(
            CreateSnapshot() with
            {
                LauncherTopmost = false
            },
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        path.Should().Be(ToolbarInteractionRetouchPath.DirectDriftRepair);
    }

    [Fact]
    public void Resolve_ShouldReturnDirectDriftRepair_WhenLauncherAndToolbarBothDrift()
    {
        var path = ToolbarInteractionRetouchPathPolicy.Resolve(
            CreateSnapshot() with
            {
                LauncherTopmost = false,
                ToolbarTopmost = false
            },
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        path.Should().Be(ToolbarInteractionRetouchPath.DirectDriftRepair);
    }

    [Fact]
    public void Resolve_ShouldReturnDirectDriftRepair_WhenLauncherAndRollCallBothDrift()
    {
        var path = ToolbarInteractionRetouchPathPolicy.Resolve(
            CreateSnapshot() with
            {
                LauncherTopmost = false,
                RollCallVisible = true,
                RollCallTopmost = false
            },
            new ToolbarInteractionRetouchDecision(
                ShouldRetouch: true,
                ForceEnforceZOrder: false,
                Reason: ToolbarInteractionRetouchDecisionReason.None));

        path.Should().Be(ToolbarInteractionRetouchPath.DirectDriftRepair);
    }

    private static ToolbarInteractionRetouchSnapshot CreateSnapshot()
    {
        return new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: true,
            WhiteboardActive: false,
            ToolbarVisible: true,
            ToolbarTopmost: true,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: true);
    }
}
