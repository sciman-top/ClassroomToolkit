using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayUpdateDispatchFailureFallbackPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenDispatchScheduled()
    {
        var decision = CrossPageDisplayUpdateDispatchFailureFallbackPolicy.Resolve(
            dispatchScheduled: true,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeFalse();
        decision.ShouldQueueReplay.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageDisplayUpdateDispatchFailureFallbackReason.None);
    }

    [Fact]
    public void Resolve_ShouldRunInline_WhenDispatchFailedOnUiThread()
    {
        var decision = CrossPageDisplayUpdateDispatchFailureFallbackPolicy.Resolve(
            dispatchScheduled: false,
            dispatcherCheckAccess: true,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeTrue();
        decision.ShouldQueueReplay.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageDisplayUpdateDispatchFailureFallbackReason.InlineCurrentThread);
    }

    [Fact]
    public void Resolve_ShouldQueueReplay_WhenDispatchFailedOffUiThread()
    {
        var decision = CrossPageDisplayUpdateDispatchFailureFallbackPolicy.Resolve(
            dispatchScheduled: false,
            dispatcherCheckAccess: false,
            dispatcherShutdownStarted: false,
            dispatcherShutdownFinished: false);

        decision.ShouldRunInline.Should().BeFalse();
        decision.ShouldQueueReplay.Should().BeTrue();
        decision.Reason.Should().Be(CrossPageDisplayUpdateDispatchFailureFallbackReason.QueueReplay);
    }
}
