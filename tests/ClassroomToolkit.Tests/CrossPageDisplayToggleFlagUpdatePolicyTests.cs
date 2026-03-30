using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayToggleFlagUpdatePolicyTests
{
    [Fact]
    public void Resolve_ShouldSkip_WhenBothFlagsAlreadyMatchRequested()
    {
        var decision = CrossPageDisplayToggleFlagUpdatePolicy.Resolve(
            currentCrossPageDisplayEnabled: true,
            requestedEnabled: true);

        decision.ShouldApply.Should().BeFalse();
        decision.NextCrossPageDisplayEnabled.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenCurrentFlagMatchesRequested()
    {
        var decision = CrossPageDisplayToggleFlagUpdatePolicy.Resolve(
            currentCrossPageDisplayEnabled: true,
            requestedEnabled: true);

        decision.ShouldApply.Should().BeFalse();
        decision.NextCrossPageDisplayEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Resolve_ShouldSetBothFlagsToRequested_WhenApplyRequired(bool requested)
    {
        var decision = CrossPageDisplayToggleFlagUpdatePolicy.Resolve(
            currentCrossPageDisplayEnabled: !requested,
            requestedEnabled: requested);

        decision.ShouldApply.Should().BeTrue();
        decision.NextCrossPageDisplayEnabled.Should().Be(requested);
    }
}
