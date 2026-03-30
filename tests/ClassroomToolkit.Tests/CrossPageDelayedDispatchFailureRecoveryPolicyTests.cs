using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDelayedDispatchFailureRecoveryPolicyTests
{
    [Fact]
    public void Resolve_ShouldDisableInline_WhenRecoveryAlreadyScheduled()
    {
        var decision = CrossPageDelayedDispatchFailureRecoveryPolicy.Resolve(
            recoveryDispatchScheduled: true,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRecoverInline.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableInline_WhenDispatcherIsShuttingDown()
    {
        var decision = CrossPageDelayedDispatchFailureRecoveryPolicy.Resolve(
            recoveryDispatchScheduled: false,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: true,
            dispatcherShutdownFinished: false);

        decision.ShouldRecoverInline.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldEnableInline_WhenUiThreadAvailableAndNotShuttingDown()
    {
        var decision = CrossPageDelayedDispatchFailureRecoveryPolicy.Resolve(
            recoveryDispatchScheduled: false,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRecoverInline.Should().BeTrue();
    }
}
