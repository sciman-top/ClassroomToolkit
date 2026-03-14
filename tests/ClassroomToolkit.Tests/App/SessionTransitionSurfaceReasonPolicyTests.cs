using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionSurfaceReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "surface-retouch-requested")]
    [InlineData(2, "no-surface-retouch-requested")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        SessionTransitionSurfaceReasonPolicy.ResolveTag((SessionTransitionSurfaceReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
