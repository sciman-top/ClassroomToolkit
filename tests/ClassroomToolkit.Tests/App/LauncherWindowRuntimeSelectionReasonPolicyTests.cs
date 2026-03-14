using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherWindowRuntimeSelectionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "prefer-main-visible")]
    [InlineData(2, "prefer-bubble-visible")]
    [InlineData(3, "fallback-main-bubble-hidden")]
    [InlineData(4, "fallback-bubble-main-hidden")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        LauncherWindowRuntimeSelectionReasonPolicy.ResolveTag((LauncherWindowRuntimeSelectionReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
