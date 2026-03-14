using ClassroomToolkit.App;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LauncherAutoExitTimerPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnStopPlan_WhenSecondsIsZero()
    {
        var plan = LauncherAutoExitTimerPlanPolicy.Resolve(autoExitSeconds: 0);

        plan.ShouldStart.Should().BeFalse();
        plan.Interval.Should().Be(System.TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_ShouldReturnStopPlan_WhenSecondsIsNegative()
    {
        var plan = LauncherAutoExitTimerPlanPolicy.Resolve(autoExitSeconds: -30);

        plan.ShouldStart.Should().BeFalse();
        plan.Interval.Should().Be(System.TimeSpan.Zero);
    }

    [Fact]
    public void Resolve_ShouldReturnStartPlan_WhenSecondsIsPositive()
    {
        var plan = LauncherAutoExitTimerPlanPolicy.Resolve(autoExitSeconds: 90);

        plan.ShouldStart.Should().BeTrue();
        plan.Interval.Should().Be(System.TimeSpan.FromSeconds(90));
    }
}
