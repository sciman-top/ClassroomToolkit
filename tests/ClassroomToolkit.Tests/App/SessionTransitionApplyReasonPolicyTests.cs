using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionApplyReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "ensure-floating-requested")]
    [InlineData(2, "scene-changed")]
    [InlineData(3, "widget-became-visible")]
    [InlineData(4, "no-apply-requested")]
    [InlineData(5, "widget-visibility-changed-without-visible")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        SessionTransitionApplyReasonPolicy.ResolveTag((SessionTransitionApplyReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
