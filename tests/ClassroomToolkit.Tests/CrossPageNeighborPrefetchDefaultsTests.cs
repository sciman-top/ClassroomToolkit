using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborPrefetchDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageNeighborPrefetchDefaults.RadiusDefault.Should().Be(2);
        CrossPageNeighborPrefetchDefaults.RadiusMin.Should().Be(1);
        CrossPageNeighborPrefetchDefaults.RadiusMax.Should().Be(4);
        CrossPageNeighborPrefetchDefaults.NeighborInkCacheLimit.Should().Be(10);
    }
}
