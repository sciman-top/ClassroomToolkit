using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchThrottleReasonPolicyTests
{
    [Theory]
    [InlineData(0, "allow")]
    [InlineData(1, "first-retouch")]
    [InlineData(2, "interval-disabled")]
    [InlineData(3, "within-throttle-window")]
    [InlineData(4, "outside-throttle-window")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expected)
    {
        ToolbarInteractionRetouchThrottleReasonPolicy.ResolveTag((ToolbarInteractionRetouchThrottleReason)reasonValue)
            .Should()
            .Be(expected);
    }
}
