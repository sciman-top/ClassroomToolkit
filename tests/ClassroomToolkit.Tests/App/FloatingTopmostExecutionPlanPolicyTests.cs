using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostExecutionPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldMapTopmostFlags_AndEnforceZOrder()
    {
        var source = new FloatingTopmostPlan(
            ToolbarTopmost: true,
            RollCallTopmost: false,
            LauncherTopmost: true,
            ImageManagerTopmost: false,
            OverlayShouldActivate: true);

        var plan = FloatingTopmostExecutionPlanPolicy.Resolve(source, enforceZOrder: true);

        plan.ToolbarTopmost.Should().BeTrue();
        plan.RollCallTopmost.Should().BeFalse();
        plan.LauncherTopmost.Should().BeTrue();
        plan.ImageManagerTopmost.Should().BeFalse();
        plan.EnforceZOrder.Should().BeTrue();
    }
}
