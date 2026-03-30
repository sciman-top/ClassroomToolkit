using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageBrushContinuationBridgeDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CrossPageBrushContinuationBridgeDefaults.DeltaYEpsilon.Should().Be(0.01);
        CrossPageBrushContinuationBridgeDefaults.InterpolationLowerExclusive.Should().Be(0.0);
        CrossPageBrushContinuationBridgeDefaults.InterpolationUpperExclusive.Should().Be(1.0);
        CrossPageBrushContinuationBridgeDefaults.SeedTimestampIncrementTicks.Should().Be(1);
    }
}
