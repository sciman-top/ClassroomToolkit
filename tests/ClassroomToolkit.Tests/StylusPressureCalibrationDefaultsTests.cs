using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusPressureCalibrationDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        StylusPressureCalibrationDefaults.BinCount.Should().Be(64);
        StylusPressureCalibrationDefaults.MinSamplesForQuantiles.Should().Be(20);
        StylusPressureCalibrationDefaults.SeedRangeMinWidth.Should().Be(0.01);
        StylusPressureCalibrationDefaults.EmaAlpha.Should().Be(0.03);
        StylusPressureCalibrationDefaults.LowQuantile.Should().Be(0.04);
        StylusPressureCalibrationDefaults.HighQuantile.Should().Be(0.96);
        StylusPressureCalibrationDefaults.MinEffectiveRange.Should().Be(0.04);
    }
}
