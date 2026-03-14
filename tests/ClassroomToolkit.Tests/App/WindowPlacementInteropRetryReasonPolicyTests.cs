using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowPlacementInteropRetryReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "max-attempts-reached")]
    [InlineData(2, "invalid-handle-error")]
    [InlineData(3, "retryable-error")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        WindowPlacementInteropRetryReasonPolicy.ResolveTag((WindowPlacementInteropRetryReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
