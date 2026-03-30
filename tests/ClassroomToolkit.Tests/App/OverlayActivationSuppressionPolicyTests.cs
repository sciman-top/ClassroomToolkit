using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayActivationSuppressionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnSuppressionRequested_WhenSuppressionFlagIsTrue()
    {
        var decision = OverlayActivationSuppressionPolicy.Resolve(true);

        decision.ShouldSuppress.Should().BeTrue();
        decision.Reason.Should().Be(OverlayActivationSuppressionReason.SuppressionRequested);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenSuppressionFlagIsFalse()
    {
        var decision = OverlayActivationSuppressionPolicy.Resolve(false);

        decision.ShouldSuppress.Should().BeFalse();
        decision.Reason.Should().Be(OverlayActivationSuppressionReason.None);
    }

    [Fact]
    public void ShouldSuppress_ShouldMapResolveDecision()
    {
        var suppress = OverlayActivationSuppressionPolicy.ShouldSuppress(true);

        suppress.Should().BeTrue();
    }
}
