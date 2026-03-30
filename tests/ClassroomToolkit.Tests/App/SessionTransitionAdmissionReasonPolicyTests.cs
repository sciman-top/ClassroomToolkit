using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionAdmissionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "accepted")]
    [InlineData(1, "no-state-change")]
    [InlineData(2, "duplicate-transition-id")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        var reason = (SessionTransitionAdmissionReason)reasonValue;
        SessionTransitionAdmissionReasonPolicy.ResolveTag(reason).Should().Be(expectedTag);
    }
}
