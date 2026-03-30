using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ToolbarInteractionRetouchAdmissionReasonPolicyTests
{
    [Theory]
    [InlineData(0, "accepted")]
    [InlineData(1, "reentry-blocked")]
    public void ResolveTag_ShouldReturnExpectedTag(int reasonValue, string expectedTag)
    {
        var reason = (ToolbarInteractionRetouchAdmissionReason)reasonValue;
        ToolbarInteractionRetouchAdmissionReasonPolicy.ResolveTag(reason).Should().Be(expectedTag);
    }
}
