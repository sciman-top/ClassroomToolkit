using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionDuplicateReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "transition-advanced")]
    [InlineData(2, "duplicate-transition-id")]
    [InlineData(3, "regressed-transition-id")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        SessionTransitionDuplicateReasonPolicy.ResolveTag((SessionTransitionDuplicateReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
