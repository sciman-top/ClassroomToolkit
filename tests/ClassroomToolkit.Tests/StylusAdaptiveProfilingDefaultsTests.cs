using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusAdaptiveProfilingDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMinMs.Should().Be(4);
        StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMaxMs.Should().Be(18);
        StylusAdaptiveProfilingDefaults.ObserveIntervalMinMs.Should().Be(0.2);
        StylusAdaptiveProfilingDefaults.ObserveIntervalMaxMs.Should().Be(100.0);
        StylusAdaptiveProfilingDefaults.ObserveIntervalWindowSize.Should().Be(64);
        StylusAdaptiveProfilingDefaults.ResolveRateMinSamples.Should().Be(8);
        StylusAdaptiveProfilingDefaults.HighSampleRateHzThreshold.Should().Be(150.0);
        StylusAdaptiveProfilingDefaults.MediumSampleRateHzThreshold.Should().Be(90.0);
        StylusAdaptiveProfilingDefaults.LowRatePredictionHorizonDeltaMs.Should().Be(4);
        StylusAdaptiveProfilingDefaults.MediumRatePredictionHorizonDeltaMs.Should().Be(2);
        StylusAdaptiveProfilingDefaults.HighRatePredictionHorizonDeltaMs.Should().Be(1);
        StylusAdaptiveProfilingDefaults.HighRatePredictionHorizonMinMs.Should().Be(6);
    }
}
