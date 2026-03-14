using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePointerUpDeferredStatePolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepFlags_WhenDeferredIsOff()
    {
        var result = CrossPagePointerUpDeferredStatePolicy.Resolve(
            deferredByInkInput: false,
            crossPageDisplayActive: true);

        result.NextDeferredByInkInput.Should().BeFalse();
        result.DeferredRefreshRequested.Should().BeFalse();
        result.ShouldLogStableRecover.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldConsumeAndRequestRefresh_WhenDeferredOnAndCrossPageActive()
    {
        var result = CrossPagePointerUpDeferredStatePolicy.Resolve(
            deferredByInkInput: true,
            crossPageDisplayActive: true);

        result.NextDeferredByInkInput.Should().BeFalse();
        result.DeferredRefreshRequested.Should().BeTrue();
        result.ShouldLogStableRecover.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldConsumeWithoutRefresh_WhenDeferredOnButCrossPageInactive()
    {
        var result = CrossPagePointerUpDeferredStatePolicy.Resolve(
            deferredByInkInput: true,
            crossPageDisplayActive: false);

        result.NextDeferredByInkInput.Should().BeFalse();
        result.DeferredRefreshRequested.Should().BeTrue();
        result.ShouldLogStableRecover.Should().BeFalse();
    }
}
