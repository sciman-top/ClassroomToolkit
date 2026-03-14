using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerActivationReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "not-topmost-target")]
    [InlineData(2, "already-active")]
    [InlineData(3, "blocked-by-toolbar")]
    [InlineData(4, "blocked-by-rollcall")]
    [InlineData(5, "blocked-by-launcher")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        ImageManagerActivationReasonPolicy.ResolveTag((ImageManagerActivationReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
