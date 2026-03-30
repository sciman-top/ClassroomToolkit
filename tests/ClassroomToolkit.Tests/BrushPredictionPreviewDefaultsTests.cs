using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class BrushPredictionPreviewDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        BrushPredictionPreviewDefaults.MinPredictionDtSeconds.Should().Be(1e-6);
        BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor.Should().Be(0.68);
        BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor.Should().Be(0.32);
        BrushPredictionPreviewDefaults.MinSpeedDipPerSec.Should().Be(12.0);
        BrushPredictionPreviewDefaults.DampingSpeedReference.Should().Be(2600.0);
        BrushPredictionPreviewDefaults.DampingMin.Should().Be(0.72);
        BrushPredictionPreviewDefaults.FirstLeadHorizonRatio.Should().Be(0.45);
        BrushPredictionPreviewDefaults.SecondLeadHorizonRatio.Should().Be(0.95);
        BrushPredictionPreviewDefaults.FirstLeadDistanceRatio.Should().Be(0.7);
        BrushPredictionPreviewDefaults.SpeedFactorRange.Should().Be(620.0);
        BrushPredictionPreviewDefaults.BaseWidthFactor.Should().Be(0.17);
        BrushPredictionPreviewDefaults.SpeedWidthGainFactor.Should().Be(0.09);
        BrushPredictionPreviewDefaults.MinBaseWidthDip.Should().Be(0.95);
        BrushPredictionPreviewDefaults.MidWidthRatio.Should().Be(0.82);
        BrushPredictionPreviewDefaults.TipWidthRatio.Should().Be(0.68);
        BrushPredictionPreviewDefaults.MinMidWidthDip.Should().Be(0.8);
        BrushPredictionPreviewDefaults.MinTipWidthDip.Should().Be(0.7);
        BrushPredictionPreviewDefaults.InitialBaseWidthFactor.Should().Be(0.2);
        BrushPredictionPreviewDefaults.InitialBaseWidthMinDip.Should().Be(0.9);
        BrushPredictionPreviewDefaults.InitialTipWidthRatio.Should().Be(0.78);
        BrushPredictionPreviewDefaults.PrimaryAlphaMultiplier.Should().Be(0.34);
        BrushPredictionPreviewDefaults.SecondaryAlphaMultiplier.Should().Be(0.24);
        BrushPredictionPreviewDefaults.TipAlphaMultiplier.Should().Be(0.18);
        BrushPredictionPreviewDefaults.TipRadiusRatio.Should().Be(0.5);
    }
}
