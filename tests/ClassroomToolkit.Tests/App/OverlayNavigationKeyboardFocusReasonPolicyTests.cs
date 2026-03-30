using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayNavigationKeyboardFocusReasonPolicyTests
{
    [Theory]
    [InlineData(0, "focus-keyboard")]
    [InlineData(1, "overlay-not-visible")]
    [InlineData(2, "blocked-by-toolbar")]
    [InlineData(3, "blocked-by-rollcall")]
    [InlineData(4, "blocked-by-image-manager")]
    [InlineData(5, "blocked-by-launcher")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expected)
    {
        OverlayNavigationKeyboardFocusReasonPolicy.ResolveTag((OverlayNavigationKeyboardFocusReason)reasonValue)
            .Should()
            .Be(expected);
    }
}
