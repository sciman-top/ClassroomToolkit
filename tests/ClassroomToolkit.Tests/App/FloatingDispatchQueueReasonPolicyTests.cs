using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingDispatchQueueReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "queued-new-request")]
    [InlineData(2, "merged-into-queued-request")]
    [InlineData(3, "queue-dispatch-failed")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingDispatchQueueReasonPolicy.ResolveTag((FloatingDispatchQueueReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
