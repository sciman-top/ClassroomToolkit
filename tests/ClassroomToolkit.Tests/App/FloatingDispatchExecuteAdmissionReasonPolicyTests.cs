using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingDispatchExecuteAdmissionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "apply-queued")]
    [InlineData(2, "not-queued")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingDispatchExecuteAdmissionReasonPolicy.ResolveTag((FloatingDispatchExecuteAdmissionReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
