using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibleChangedApplyReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "bubble-hidden")]
    [InlineData(2, "visible-changed-suppressed")]
    [InlineData(3, "cooldown-active")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        LauncherBubbleVisibleChangedApplyReasonPolicy.ResolveTag((LauncherBubbleVisibleChangedApplyReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
