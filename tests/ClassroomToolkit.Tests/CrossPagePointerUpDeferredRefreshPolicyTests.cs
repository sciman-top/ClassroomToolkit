using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePointerUpDeferredRefreshPolicyTests
{
    [Fact]
    public void Resolve_ShouldNotConsume_WhenDeferredFlagIsOff()
    {
        var decision = CrossPagePointerUpDeferredRefreshPolicy.Resolve(
            deferredByInkInput: false,
            crossPageDisplayActive: true);

        decision.ShouldConsumeDeferredFlag.Should().BeFalse();
        decision.ShouldRequestPostRefresh.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldConsumeAndRequest_WhenDeferredFlagOnAndSceneEligible()
    {
        var decision = CrossPagePointerUpDeferredRefreshPolicy.Resolve(
            deferredByInkInput: true,
            crossPageDisplayActive: true);

        decision.ShouldConsumeDeferredFlag.Should().BeTrue();
        decision.ShouldRequestPostRefresh.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldConsumeWithoutRequest_WhenDeferredFlagOnButSceneIneligible()
    {
        var decision = CrossPagePointerUpDeferredRefreshPolicy.Resolve(
            deferredByInkInput: true,
            crossPageDisplayActive: false);

        decision.ShouldConsumeDeferredFlag.Should().BeTrue();
        decision.ShouldRequestPostRefresh.Should().BeFalse();
    }
}
