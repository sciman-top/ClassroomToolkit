using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ForegroundZOrderRetouchReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "force-disabled-by-design")]
    [InlineData(2, "overlay-visible-presentation")]
    [InlineData(3, "overlay-hidden-presentation")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        ForegroundZOrderRetouchReasonPolicy.ResolveTag((ForegroundZOrderRetouchReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
