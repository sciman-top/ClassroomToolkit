using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CalligraphyRenderingDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        CalligraphyRenderingDefaults.SealStrokeWidthFactor.Should().Be(0.08);
        CalligraphyRenderingDefaults.DegradeAreaThreshold.Should().Be(160000.0);
        CalligraphyRenderingDefaults.DegradeLayerThreshold.Should().Be(22);
        CalligraphyRenderingDefaults.MaxRibbonLayersNormal.Should().Be(18);
        CalligraphyRenderingDefaults.MaxRibbonLayersDegraded.Should().Be(8);
        CalligraphyRenderingDefaults.MaxBloomLayersNormal.Should().Be(10);
        CalligraphyRenderingDefaults.MaxBloomLayersDegraded.Should().Be(4);
        CalligraphyRenderingDefaults.AdaptiveLevelMax.Should().Be(2);
        CalligraphyRenderingDefaults.AdaptiveHighCostMs.Should().Be(6.4);
        CalligraphyRenderingDefaults.AdaptiveLowCostMs.Should().Be(3.8);
        CalligraphyRenderingDefaults.AdaptiveCostEmaAlpha.Should().Be(0.2);
        CalligraphyRenderingDefaults.AdaptiveAreaThresholdStep.Should().Be(25000);
        CalligraphyRenderingDefaults.AdaptiveLayerThresholdStep.Should().Be(4);
    }
}
