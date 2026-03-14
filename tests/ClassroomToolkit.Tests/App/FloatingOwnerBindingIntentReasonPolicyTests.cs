using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingOwnerBindingIntentReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "toolbar-owner-binding-requested")]
    [InlineData(2, "rollcall-owner-binding-requested")]
    [InlineData(3, "image-manager-owner-binding-requested")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingOwnerBindingIntentReasonPolicy.ResolveTag((FloatingOwnerBindingIntentReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
