using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingActivationGuardReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "toolbar-active")]
    [InlineData(2, "rollcall-active")]
    [InlineData(3, "image-manager-active")]
    [InlineData(4, "launcher-active")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingActivationGuardReasonPolicy.ResolveTag((FloatingActivationGuardReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
