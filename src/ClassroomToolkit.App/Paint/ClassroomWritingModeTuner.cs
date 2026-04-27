using System;
using ClassroomToolkit.App.Paint.Brushes;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct ClassroomWritingProfile(
    double MarkerPressureMultiplier,
    double CalligraphyPressureInfluenceMultiplier,
    double CalligraphyPressureScaleMultiplier,
    double PseudoPressureLowThreshold,
    double PseudoPressureHighThreshold,
    double CalligraphyPreviewMinDistance);

internal readonly record struct ClassroomRuntimeSettings(
    double PseudoPressureLowThreshold,
    double PseudoPressureHighThreshold,
    double CalligraphyPreviewMinDistance);

internal static class ClassroomWritingModeTuner
{
    public const double DefaultCalligraphyPreviewMinDistance = 2.0;
    public const double DefaultPseudoPressureLowThreshold = 0.0001;
    public const double DefaultPseudoPressureHighThreshold = 0.9999;

    private const double MarkerPressureFactorMin = 0.02;
    private const double MarkerPressureFactorMax = 0.45;
    private const double CalligraphyInfluenceMin = 0.2;
    private const double CalligraphyInfluenceMax = 0.9;
    private const double CalligraphyScaleMin = 0.12;
    private const double CalligraphyScaleMax = 0.55;
    private const double PseudoPressureLowMin = 0.0;
    private const double PseudoPressureLowMax = 0.49;
    private const double PseudoPressureHighMinGap = 0.001;
    private const double PseudoPressureHighMax = 1.0;
    private const double CalligraphyPreviewMin = 1.0;
    private const double CalligraphyPreviewMax = 4.0;

    public static ClassroomWritingProfile ResolveProfile(ClassroomWritingMode mode)
    {
        return mode switch
        {
            ClassroomWritingMode.Stable => new ClassroomWritingProfile(
                MarkerPressureMultiplier: 0.85,
                CalligraphyPressureInfluenceMultiplier: 0.7,
                CalligraphyPressureScaleMultiplier: 0.66,
                PseudoPressureLowThreshold: 0.0002,
                PseudoPressureHighThreshold: 0.9998,
                CalligraphyPreviewMinDistance: 2.6),
            ClassroomWritingMode.Responsive => new ClassroomWritingProfile(
                MarkerPressureMultiplier: 1.18,
                CalligraphyPressureInfluenceMultiplier: 1.3,
                CalligraphyPressureScaleMultiplier: 1.28,
                PseudoPressureLowThreshold: 0.00005,
                PseudoPressureHighThreshold: 0.99995,
                CalligraphyPreviewMinDistance: 1.4),
            _ => new ClassroomWritingProfile(
                MarkerPressureMultiplier: 1.0,
                CalligraphyPressureInfluenceMultiplier: 1.0,
                CalligraphyPressureScaleMultiplier: 1.0,
                PseudoPressureLowThreshold: DefaultPseudoPressureLowThreshold,
                PseudoPressureHighThreshold: DefaultPseudoPressureHighThreshold,
                CalligraphyPreviewMinDistance: DefaultCalligraphyPreviewMinDistance)
        };
    }

    public static ClassroomRuntimeSettings ResolveRuntimeSettings(ClassroomWritingMode mode)
    {
        var profile = ResolveProfile(mode);
        var low = Math.Clamp(profile.PseudoPressureLowThreshold, PseudoPressureLowMin, PseudoPressureLowMax);
        var high = Math.Clamp(profile.PseudoPressureHighThreshold, low + PseudoPressureHighMinGap, PseudoPressureHighMax);
        var previewDistance = Math.Clamp(profile.CalligraphyPreviewMinDistance, CalligraphyPreviewMin, CalligraphyPreviewMax);
        return new ClassroomRuntimeSettings(low, high, previewDistance);
    }

    public static void ApplyToMarkerConfig(MarkerBrushConfig config, ClassroomWritingMode mode)
    {
        ArgumentNullException.ThrowIfNull(config);

        var profile = ResolveProfile(mode);
        config.PressureWidthFactor = Math.Clamp(
            config.PressureWidthFactor * profile.MarkerPressureMultiplier,
            MarkerPressureFactorMin,
            MarkerPressureFactorMax);
    }

    public static void ApplyToCalligraphyConfig(BrushPhysicsConfig config, ClassroomWritingMode mode)
    {
        ArgumentNullException.ThrowIfNull(config);

        var profile = ResolveProfile(mode);
        config.RealPressureWidthInfluence = Math.Clamp(
            config.RealPressureWidthInfluence * profile.CalligraphyPressureInfluenceMultiplier,
            CalligraphyInfluenceMin,
            CalligraphyInfluenceMax);
        config.RealPressureWidthScale = Math.Clamp(
            config.RealPressureWidthScale * profile.CalligraphyPressureScaleMultiplier,
            CalligraphyScaleMin,
            CalligraphyScaleMax);
    }

    public static bool TryResolveStylusPressure(
        double rawPressure,
        double lowThreshold,
        double highThreshold,
        out double resolvedPressure)
    {
        resolvedPressure = 0.0;
        if (!double.IsFinite(rawPressure))
        {
            return false;
        }

        var low = Math.Clamp(lowThreshold, PseudoPressureLowMin, PseudoPressureLowMax);
        var high = Math.Clamp(highThreshold, low + PseudoPressureHighMinGap, PseudoPressureHighMax);
        if (rawPressure <= low || rawPressure >= high)
        {
            return false;
        }

        resolvedPressure = Math.Clamp(rawPressure, 0.0, 1.0);
        return true;
    }
}
