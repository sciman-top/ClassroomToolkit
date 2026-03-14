using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivationReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "overlay-hidden")]
    [InlineData(2, "surface-not-activatable")]
    [InlineData(3, "overlay-already-active")]
    [InlineData(4, "blocked-by-toolbar")]
    [InlineData(5, "blocked-by-rollcall")]
    [InlineData(6, "blocked-by-image-manager")]
    [InlineData(7, "blocked-by-launcher")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        OverlayActivationReasonPolicy.ResolveTag((OverlayActivationReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
