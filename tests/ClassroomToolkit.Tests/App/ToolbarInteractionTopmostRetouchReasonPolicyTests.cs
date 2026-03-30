using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionTopmostRetouchReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "overlay-hidden")]
    [InlineData(2, "scene-not-interactive")]
    [InlineData(3, "interactive-scene-active")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        ToolbarInteractionTopmostRetouchReasonPolicy.ResolveTag((ToolbarInteractionTopmostRetouchReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
