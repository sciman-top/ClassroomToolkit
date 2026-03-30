using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkCacheUpdateTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldStartMonitor_WhenMonitorDisabled()
    {
        var plan = InkCacheUpdateTransitionPolicy.Resolve(
            enabled: true,
            monitorEnabled: false);

        plan.ShouldStartMonitor.Should().BeTrue();
        plan.ShouldClearCache.Should().BeFalse();
        plan.ShouldRequestRefresh.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldClearCache_WhenDisabled()
    {
        var plan = InkCacheUpdateTransitionPolicy.Resolve(
            enabled: false,
            monitorEnabled: true);

        plan.ShouldStartMonitor.Should().BeFalse();
        plan.ShouldClearCache.Should().BeTrue();
        plan.ShouldRequestRefresh.Should().BeTrue();
    }
}
