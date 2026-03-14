using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingActivationIntentReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "overlay-activation-requested")]
    [InlineData(2, "image-manager-activation-requested")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        FloatingActivationIntentReasonPolicy.ResolveTag((FloatingActivationIntentReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
