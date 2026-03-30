using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingOwnerBindingReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "attach-when-overlay-visible")]
    [InlineData(2, "detach-when-overlay-hidden")]
    [InlineData(3, "already-aligned")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingOwnerBindingReasonPolicy.ResolveTag((FloatingOwnerBindingReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
