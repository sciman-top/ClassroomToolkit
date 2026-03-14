namespace ClassroomToolkit.App.Paint;

internal static class BrushPredictionPreviewDefaults
{
    internal const double MinPredictionDtSeconds = 1e-6;
    internal const double VelocitySmoothingKeepFactor = 0.68;
    internal const double VelocitySmoothingApplyFactor = 0.32;
    internal const double MinSpeedDipPerSec = 12.0;
    internal const double DampingSpeedReference = 2600.0;
    internal const double DampingMin = 0.72;
    internal const double FirstLeadHorizonRatio = 0.45;
    internal const double SecondLeadHorizonRatio = 0.95;
    internal const double FirstLeadDistanceRatio = 0.7;
    internal const double SpeedFactorRange = 620.0;
    internal const double BaseWidthFactor = 0.17;
    internal const double SpeedWidthGainFactor = 0.09;
    internal const double MinBaseWidthDip = 0.95;
    internal const double MidWidthRatio = 0.82;
    internal const double TipWidthRatio = 0.68;
    internal const double MinMidWidthDip = 0.8;
    internal const double MinTipWidthDip = 0.7;
    internal const double InitialBaseWidthFactor = 0.2;
    internal const double InitialBaseWidthMinDip = 0.9;
    internal const double InitialTipWidthRatio = 0.78;
    internal const double PrimaryAlphaMultiplier = 0.34;
    internal const double SecondaryAlphaMultiplier = 0.24;
    internal const double TipAlphaMultiplier = 0.18;
    internal const double TipRadiusRatio = 0.5;
}
