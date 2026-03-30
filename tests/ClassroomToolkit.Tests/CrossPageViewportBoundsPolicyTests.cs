using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageViewportBoundsPolicyTests
{
    [Fact]
    public void ResolveSlackDip_ShouldRespectMinimumAndRatio()
    {
        CrossPageViewportBoundsPolicy.ResolveSlackDip(40).Should().Be(32.0);
        CrossPageViewportBoundsPolicy.ResolveSlackDip(200).Should().Be(100.0);
    }

    [Fact]
    public void IsTranslateClamped_ShouldUseConfiguredEpsilon()
    {
        CrossPageViewportBoundsPolicy.IsTranslateClamped(100.0, 100.4).Should().BeFalse();
        CrossPageViewportBoundsPolicy.IsTranslateClamped(100.0, 100.6).Should().BeTrue();
    }
}
