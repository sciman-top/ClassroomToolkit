using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayNavigationActivateReasonPolicyTests
{
    [Theory]
    [InlineData(0, "activate")]
    [InlineData(1, "avoid-activate-requested")]
    [InlineData(2, "overlay-already-active")]
    [InlineData(3, "blocked-by-toolbar")]
    [InlineData(4, "blocked-by-rollcall")]
    [InlineData(5, "blocked-by-image-manager")]
    [InlineData(6, "blocked-by-launcher")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expected)
    {
        OverlayNavigationActivateReasonPolicy.ResolveTag((OverlayNavigationActivateReason)reasonValue)
            .Should()
            .Be(expected);
    }
}
