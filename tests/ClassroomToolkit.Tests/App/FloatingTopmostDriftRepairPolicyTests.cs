using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostDriftRepairPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNoRepairs_WhenAllVisibleWindowsAreTopmost()
    {
        var plan = FloatingTopmostDriftRepairPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: true,
                RollCallVisible: true,
                RollCallTopmost: true,
                LauncherVisible: true,
                LauncherTopmost: true));

        plan.RepairToolbar.Should().BeFalse();
        plan.RepairRollCall.Should().BeFalse();
        plan.RepairLauncher.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRepairOnlyDriftedVisibleWindows()
    {
        var plan = FloatingTopmostDriftRepairPolicy.Resolve(
            new ToolbarInteractionRetouchSnapshot(
                OverlayVisible: true,
                PhotoModeActive: true,
                WhiteboardActive: false,
                ToolbarVisible: true,
                ToolbarTopmost: false,
                RollCallVisible: false,
                RollCallTopmost: false,
                LauncherVisible: true,
                LauncherTopmost: false));

        plan.RepairToolbar.Should().BeTrue();
        plan.RepairRollCall.Should().BeFalse();
        plan.RepairLauncher.Should().BeTrue();
    }
}
