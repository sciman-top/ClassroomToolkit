using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivationSuppressionReasonPolicyTests
{
    [Fact]
    public void ResolveTag_ShouldReturnExpectedTag()
    {
        OverlayActivationSuppressionReasonPolicy.ResolveTag(OverlayActivationSuppressionReason.SuppressionRequested)
            .Should().Be("suppression-requested");
        OverlayActivationSuppressionReasonPolicy.ResolveTag(OverlayActivationSuppressionReason.None)
            .Should().Be("not-suppressed");
    }
}
