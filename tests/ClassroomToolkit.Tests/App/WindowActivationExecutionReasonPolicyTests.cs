using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowActivationExecutionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "execution-not-requested")]
    [InlineData(2, "target-missing")]
    [InlineData(3, "executed")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        WindowActivationExecutionReasonPolicy.ResolveTag((WindowActivationExecutionReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
