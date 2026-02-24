using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StylusPressureCurveCalibratorTests
{
    [Fact]
    public void Calibrate_ShouldKeepRawValue_ForNonContinuousProfile()
    {
        var calibrator = new StylusPressureCurveCalibrator();

        var value = calibrator.Calibrate(0.62, StylusPressureDeviceProfile.LowRange);

        value.Should().BeApproximately(0.62, 0.0001);
    }

    [Fact]
    public void Calibrate_ShouldStretchCompressedRange_ForContinuousProfile()
    {
        var calibrator = new StylusPressureCurveCalibrator();
        for (int i = 0; i < 120; i++)
        {
            var sample = 0.44 + (i % 20) * 0.008; // [0.44, 0.592]
            calibrator.Calibrate(sample, StylusPressureDeviceProfile.Continuous);
        }

        var low = calibrator.Calibrate(0.445, StylusPressureDeviceProfile.Continuous);
        var high = calibrator.Calibrate(0.588, StylusPressureDeviceProfile.Continuous);

        low.Should().BeLessThan(0.2);
        high.Should().BeGreaterThan(0.8);
    }

    [Fact]
    public void Calibrate_ShouldUseSeedRange_BeforeEnoughSamples()
    {
        var calibrator = new StylusPressureCurveCalibrator();
        calibrator.SeedRange(0.2, 0.8);

        var low = calibrator.Calibrate(0.2, StylusPressureDeviceProfile.Continuous);
        var high = calibrator.Calibrate(0.8, StylusPressureDeviceProfile.Continuous);

        low.Should().BeApproximately(0.0, 0.02);
        high.Should().BeApproximately(1.0, 0.02);
    }
}
