using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherTopmostVisibilityReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "main-visible")]
    [InlineData(2, "main-hidden-or-minimized")]
    [InlineData(3, "bubble-visible")]
    [InlineData(4, "bubble-hidden-or-minimized")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        LauncherTopmostVisibilityReasonPolicy.ResolveTag((LauncherTopmostVisibilityReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
