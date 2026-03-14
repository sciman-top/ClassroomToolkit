using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayRunGatePolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowRun_WhenDisplayIsActive()
    {
        var decision = CrossPageDisplayRunGatePolicy.Resolve(crossPageDisplayActive: true);

        decision.ShouldRun.Should().BeTrue();
        decision.AbortReason.Should().BeNull();
    }

    [Fact]
    public void Resolve_ShouldBlockRun_WhenDisplayIsInactive()
    {
        var decision = CrossPageDisplayRunGatePolicy.Resolve(crossPageDisplayActive: false);

        decision.ShouldRun.Should().BeFalse();
        decision.AbortReason.Should().Be(CrossPageDeferredDiagnosticReason.Inactive);
    }
}
