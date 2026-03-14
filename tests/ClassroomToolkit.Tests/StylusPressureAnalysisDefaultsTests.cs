using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusPressureAnalysisDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        StylusPressureAnalysisDefaults.WindowSize.Should().Be(28);
        StylusPressureAnalysisDefaults.MinSamplesForProfile.Should().Be(12);
        StylusPressureAnalysisDefaults.EndpointPseudoRatioThreshold.Should().Be(0.82);
        StylusPressureAnalysisDefaults.LowRangeThreshold.Should().Be(0.07);
        StylusPressureAnalysisDefaults.ContinuousRangeThreshold.Should().Be(0.18);
        StylusPressureAnalysisDefaults.EndpointDistinctMax.Should().Be(3);
        StylusPressureAnalysisDefaults.LowRangeDistinctMax.Should().Be(4);
        StylusPressureAnalysisDefaults.ContinuousDistinctMin.Should().Be(7);
        StylusPressureAnalysisDefaults.BucketScale.Should().Be(100.0);
        StylusPressureAnalysisDefaults.EndpointRatioUpperBoundForContinuous.Should().Be(0.7);
        StylusPressureAnalysisDefaults.GammaMin.Should().Be(0.55);
        StylusPressureAnalysisDefaults.GammaMax.Should().Be(1.8);
    }
}
