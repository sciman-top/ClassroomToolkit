using System.Diagnostics;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StylusDeviceAdaptiveProfilerTests
{
    [Fact]
    public void Observe_ShouldResolveHighRateProfile_AndTuneConfigs()
    {
        var profiler = new StylusDeviceAdaptiveProfiler();
        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 180); // ~180Hz

        for (int i = 0; i < 40; i++)
        {
            now += step;
            profiler.Observe(now, StylusPressureDeviceProfile.Continuous);
        }

        profiler.CurrentProfile.SampleRateTier.Should().Be(StylusSampleRateTier.High);
        profiler.CurrentProfile.PressureProfile.Should().Be(StylusPressureDeviceProfile.Continuous);

        var marker = MarkerBrushConfig.Balanced;
        StylusDeviceAdaptiveProfiler.ApplyToMarkerConfig(marker, profiler.CurrentProfile);
        marker.MinMoveDistance.Should().BeInRange(0.35, 0.8);

        var calligraphy = BrushPhysicsConfig.CreateCalligraphyBalanced();
        StylusDeviceAdaptiveProfiler.ApplyToCalligraphyConfig(calligraphy, profiler.CurrentProfile);
        calligraphy.RealPressureWidthInfluence.Should().BeGreaterThan(0.5);
    }

    [Fact]
    public void Seed_ShouldRestoreKnownProfileAndPredictionHorizon()
    {
        var profiler = new StylusDeviceAdaptiveProfiler();

        profiler.Seed(
            StylusPressureDeviceProfile.Continuous,
            StylusSampleRateTier.Medium,
            predictionHorizonMs: 12);

        profiler.CurrentProfile.PressureProfile.Should().Be(StylusPressureDeviceProfile.Continuous);
        profiler.CurrentProfile.SampleRateTier.Should().Be(StylusSampleRateTier.Medium);
        profiler.CurrentProfile.PredictionHorizonMs.Should().Be(12);
    }
}
