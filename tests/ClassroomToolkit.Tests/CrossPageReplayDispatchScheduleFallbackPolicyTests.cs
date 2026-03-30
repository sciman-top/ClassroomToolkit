using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageReplayDispatchScheduleFallbackPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenDispatchScheduled()
    {
        var decision = CrossPageReplayDispatchScheduleFallbackPolicy.Resolve(
            dispatchScheduled: true,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeFalse();
        decision.ShouldRequeuePending.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageReplayDispatchScheduleFallbackReason.None);
    }

    [Fact]
    public void Resolve_ShouldRunInline_WhenDispatchFailedOnUiThread()
    {
        var decision = CrossPageReplayDispatchScheduleFallbackPolicy.Resolve(
            dispatchScheduled: false,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeTrue();
        decision.ShouldRequeuePending.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageReplayDispatchScheduleFallbackReason.InlineCurrentThread);
    }

    [Fact]
    public void Resolve_ShouldRequeue_WhenDispatchFailedOffUiThread()
    {
        var decision = CrossPageReplayDispatchScheduleFallbackPolicy.Resolve(
            dispatchScheduled: false,
            dispatcherCheckAccess: false,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeFalse();
        decision.ShouldRequeuePending.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageReplayDispatchScheduleFallbackReason.RequeuePending);
    }
}
