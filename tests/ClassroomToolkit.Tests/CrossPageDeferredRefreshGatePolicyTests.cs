using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDeferredRefreshGatePolicyTests
{
    [Fact]
    public void ResolveBeforeSchedule_ShouldBlock_WhenInactiveOrInteractionActive()
    {
        var inactive = CrossPageDeferredRefreshGatePolicy.ResolveBeforeSchedule(
            crossPageDisplayActive: false,
            interactionActive: false);
        var interactionActive = CrossPageDeferredRefreshGatePolicy.ResolveBeforeSchedule(
            crossPageDisplayActive: true,
            interactionActive: true);

        inactive.ShouldProceed.Should().BeFalse();
        inactive.Reason.Should().Be(CrossPageDeferredDiagnosticReason.Inactive);
        interactionActive.ShouldProceed.Should().BeFalse();
        interactionActive.Reason.Should().Be(CrossPageDeferredDiagnosticReason.InteractionActive);
    }

    [Fact]
    public void ResolveBeforeSchedule_ShouldProceed_WhenActiveAndInteractionIdle()
    {
        var decision = CrossPageDeferredRefreshGatePolicy.ResolveBeforeSchedule(
            crossPageDisplayActive: true,
            interactionActive: false);

        decision.ShouldProceed.Should().BeTrue();
        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void ResolveBeforeDelayedDispatch_ShouldUseCombinedReason_WhenBlocked()
    {
        var decision = CrossPageDeferredRefreshGatePolicy.ResolveBeforeDelayedDispatch(
            crossPageDisplayActive: false,
            interactionActive: true);

        decision.ShouldProceed.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageDeferredDiagnosticReason.InactiveOrInteractionActive);
    }
}
