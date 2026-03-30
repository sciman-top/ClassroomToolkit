using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ForegroundExplicitRetouchThrottleReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldReturnExpectedTag()
    {
        ForegroundExplicitRetouchThrottleReasonPolicy.ResolveTag(ForegroundExplicitRetouchThrottleReason.Throttled)
            .Should().Be("throttled");
        ForegroundExplicitRetouchThrottleReasonPolicy.ResolveTag(ForegroundExplicitRetouchThrottleReason.None)
            .Should().Be("allow");
    }
}
