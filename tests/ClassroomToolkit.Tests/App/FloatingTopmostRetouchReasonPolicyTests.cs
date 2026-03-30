using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostRetouchReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "overlay-topmost-not-rising")]
    [InlineData(2, "overlay-topmost-became-required")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingTopmostRetouchReasonPolicy.ResolveTag((FloatingTopmostRetouchReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
