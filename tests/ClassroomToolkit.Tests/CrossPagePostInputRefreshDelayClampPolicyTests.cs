using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPagePostInputRefreshDelayClampPolicyTests
{
    [Fact]
    public void Clamp_ShouldRespectLowerBound()
    {
        var value = CrossPagePostInputRefreshDelayClampPolicy.Clamp(0);

        value.Should().Be(CrossPagePostInputRefreshDelayClampPolicy.MinDelayMs);
    }

    [Fact]
    public void Clamp_ShouldRespectUpperBound()
    {
        var value = CrossPagePostInputRefreshDelayClampPolicy.Clamp(1000);

        value.Should().Be(CrossPagePostInputRefreshDelayClampPolicy.MaxDelayMs);
    }

    [Fact]
    public void Clamp_ShouldKeepValue_WhenWithinBounds()
    {
        var value = CrossPagePostInputRefreshDelayClampPolicy.Clamp(220);

        value.Should().Be(220);
    }
}
