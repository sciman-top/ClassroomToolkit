using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchDispatchPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnImmediate_WhenActivatedDirectRepairInInteractiveScene_AndLauncherDrifts()
    {
        var mode = ToolbarInteractionRetouchDispatchPolicy.Resolve(
            ToolbarInteractionRetouchTrigger.Activated,
            CreateSnapshot(photoModeActive: true, whiteboardActive: false),
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        mode.Should().Be(ToolbarInteractionRetouchDispatchMode.Immediate);
    }

    [Fact]
    public void Resolve_ShouldReturnBackground_WhenActivatedDirectRepairInInteractiveScene_WithoutLauncherDrift()
    {
        var mode = ToolbarInteractionRetouchDispatchPolicy.Resolve(
            ToolbarInteractionRetouchTrigger.Activated,
            CreateSnapshot(photoModeActive: true, whiteboardActive: false, launcherTopmost: true),
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        mode.Should().Be(ToolbarInteractionRetouchDispatchMode.Background);
    }

    [Fact]
    public void Resolve_ShouldReturnImmediate_WhenNonInteractiveScene()
    {
        var mode = ToolbarInteractionRetouchDispatchPolicy.Resolve(
            ToolbarInteractionRetouchTrigger.Activated,
            CreateSnapshot(photoModeActive: false, whiteboardActive: false),
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: true,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false));

        mode.Should().Be(ToolbarInteractionRetouchDispatchMode.Immediate);
    }

    [Fact]
    public void Resolve_ShouldReturnImmediate_WhenExecutionIsNotDirectRepair()
    {
        var mode = ToolbarInteractionRetouchDispatchPolicy.Resolve(
            ToolbarInteractionRetouchTrigger.Activated,
            CreateSnapshot(photoModeActive: true, whiteboardActive: false),
            new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false));

        mode.Should().Be(ToolbarInteractionRetouchDispatchMode.Immediate);
    }

    private static ToolbarInteractionRetouchSnapshot CreateSnapshot(bool photoModeActive, bool whiteboardActive, bool launcherTopmost = false)
    {
        return new ToolbarInteractionRetouchSnapshot(
            OverlayVisible: true,
            PhotoModeActive: photoModeActive,
            WhiteboardActive: whiteboardActive,
            ToolbarVisible: true,
            ToolbarTopmost: false,
            RollCallVisible: false,
            RollCallTopmost: false,
            LauncherVisible: true,
            LauncherTopmost: launcherTopmost);
    }
}
