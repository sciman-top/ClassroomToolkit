using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostForceEnforceReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "disabled-by-design")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingTopmostForceEnforceReasonPolicy.ResolveTag((FloatingTopmostForceEnforceReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
