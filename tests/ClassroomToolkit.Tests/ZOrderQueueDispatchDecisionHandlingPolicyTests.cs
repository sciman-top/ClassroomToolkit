using ClassroomToolkit.App;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class ZOrderQueueDispatchDecisionHandlingPolicyTests
{
    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, false, false)]
    [InlineData(2, true, false)]
    [InlineData(3, true, true)]
    public void Resolve_ShouldMatchExpected(
        int reason,
        bool expectedLogDecision,
        bool expectedQueueDispatchFailed)
    {
        var plan = ZOrderQueueDispatchDecisionHandlingPolicy.Resolve((FloatingDispatchQueueReason)reason);

        plan.ShouldLogDecision.Should().Be(expectedLogDecision);
        plan.ShouldMarkQueueDispatchFailed.Should().Be(expectedQueueDispatchFailed);
    }
}
