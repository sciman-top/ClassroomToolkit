using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionApplyGateReasonPolicyTests
{
    [Theory]
    [InlineData(0, "apply")]
    [InlineData(1, "no-zorder-action")]
    [InlineData(2, "touch-surface-requested")]
    [InlineData(3, "zorder-apply-requested")]
    [InlineData(4, "force-enforce-requested")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        var reason = (SessionTransitionApplyGateReason)reasonValue;
        SessionTransitionApplyGateReasonPolicy.ResolveTag(reason).Should().Be(expectedTag);
    }
}
