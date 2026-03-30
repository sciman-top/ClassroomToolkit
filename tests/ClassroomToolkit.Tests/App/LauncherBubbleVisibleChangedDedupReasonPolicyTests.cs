using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibleChangedDedupReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "duplicate-within-window")]
    [InlineData(2, "no-history")]
    [InlineData(3, "interval-disabled")]
    [InlineData(4, "unset-timestamp")]
    [InlineData(5, "applied")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        LauncherBubbleVisibleChangedDedupReasonPolicy.ResolveTag((LauncherBubbleVisibleChangedDedupReason)reasonValue)
            .Should().Be(expectedTag);
    }
}
