using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionZOrderRetouchReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "scene-changed-to-surface")]
    [InlineData(2, "scene-unchanged")]
    [InlineData(3, "scene-changed-to-none-surface")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        SessionTransitionZOrderRetouchReasonPolicy.ResolveTag((SessionTransitionZOrderRetouchReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
