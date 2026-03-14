using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherVisibilityTransitionReasonPolicyTests
{
    [Theory]
    [InlineData(1, "hide-main-and-show-bubble")]
    [InlineData(2, "hide-main-only")]
    [InlineData(3, "show-bubble-only")]
    [InlineData(4, "no-op")]
    public void ResolveMinimizeTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        LauncherVisibilityTransitionReasonPolicy.ResolveMinimizeTag((LauncherVisibilityMinimizeReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }

    [Theory]
    [InlineData(1, "show-main-and-hide-bubble")]
    [InlineData(2, "show-main-only")]
    [InlineData(3, "hide-bubble-only")]
    [InlineData(4, "no-op")]
    public void ResolveRestoreTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        LauncherVisibilityTransitionReasonPolicy.ResolveRestoreTag((LauncherVisibilityRestoreReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
