using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDuplicateWindowIntervalPolicyTests
{
    [Fact]
    public void Resolve_ShouldClampConfiguredToAtLeastOne()
    {
        var value = CrossPageDuplicateWindowIntervalPolicy.Resolve(
            configuredWindowMs: 0,
            minimumWindowMs: CrossPageDuplicateWindowThresholds.MinWindowMs);

        value.Should().Be(CrossPageDuplicateWindowThresholds.MinWindowMs);
    }

    [Fact]
    public void Resolve_ShouldUseMinimumWhenConfiguredLower()
    {
        var value = CrossPageDuplicateWindowIntervalPolicy.Resolve(
            configuredWindowMs: 8,
            minimumWindowMs: 14);

        value.Should().Be(14);
    }

    [Fact]
    public void Resolve_ShouldKeepConfiguredWhenAlreadyHigher()
    {
        var value = CrossPageDuplicateWindowIntervalPolicy.Resolve(
            configuredWindowMs: 30,
            minimumWindowMs: 14);

        value.Should().Be(30);
    }
}
