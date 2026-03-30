using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageMissingNeighborRefreshNormalizationDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs.Should().Be(1);
        CrossPageMissingNeighborRefreshNormalizationDefaults.MinMissingThreshold.Should().Be(1);
    }
}
