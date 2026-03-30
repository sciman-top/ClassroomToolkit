using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingActivationExecutionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "target-missing")]
    [InlineData(2, "activation-not-requested")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingActivationExecutionReasonPolicy.ResolveTag((FloatingActivationExecutionReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
