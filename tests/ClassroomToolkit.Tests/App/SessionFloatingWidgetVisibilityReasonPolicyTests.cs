using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionFloatingWidgetVisibilityReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "rollcall-became-visible")]
    [InlineData(2, "launcher-became-visible")]
    [InlineData(3, "toolbar-became-visible")]
    [InlineData(4, "visibility-changed-without-visible")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        SessionFloatingWidgetVisibilityReasonPolicy.ResolveTag((SessionFloatingWidgetVisibilityReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
