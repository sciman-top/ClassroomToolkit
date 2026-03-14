using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowLifecycleSubscriptionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "none")]
    [InlineData(1, "current-window-missing")]
    [InlineData(2, "same-window-instance")]
    [InlineData(3, "window-instance-changed")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        WindowLifecycleSubscriptionReasonPolicy.ResolveTag((WindowLifecycleSubscriptionReason)reasonValue)
            .Should()
            .Be(expectedTag);
    }
}
