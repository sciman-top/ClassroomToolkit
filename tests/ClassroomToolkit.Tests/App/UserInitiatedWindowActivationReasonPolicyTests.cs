using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class UserInitiatedWindowActivationReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "window-not-visible")]
    [InlineData(2, "window-already-active")]
    [InlineData(3, "activation-required")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        UserInitiatedWindowActivationReasonPolicy.ResolveTag((UserInitiatedWindowActivationReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
