using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Settings;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class BrushPredictionPreviewDefaults
{
    internal const double MinPredictionDtSeconds = 1e-6;
    internal const double VelocitySmoothingKeepFactor = 0.68;
    internal const double VelocitySmoothingApplyFactor = 0.32;
    internal const double MinSpeedDipPerSec = 12.0;
    internal const double DampingSpeedReference = 2600.0;
    internal const double DampingMin = 0.72;
    // 二阶外推（加速度项）：预测 lead = v·t·damping + ½a·t²·accelGain，
    // 加速度由平滑速度差分再 EMA 得到，并钳制幅值避免转向时甩尖。
    internal const double AccelerationKeepFactor = 0.72;
    internal const double AccelerationApplyFactor = 0.28;
    internal const double MaxAccelerationDipPerSecSq = 16000.0;
    internal const double AccelerationLeadGain = 0.85;
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

internal static class BrushPredictionVelocityPolicy
{
    internal static Vector Resolve(
        Vector currentVelocity,
        BrushInputSample previous,
        BrushInputSample current)
    {
        var dtMs = (current.TimestampTicks - previous.TimestampTicks)
            * 1000.0
            / Math.Max(Stopwatch.Frequency, 1);
        if (dtMs < InkInputRuntimeDefaults.PredictionUpdateMinDtMs)
        {
            return currentVelocity;
        }

        var dtSeconds = dtMs / 1000.0;
        var measured = (current.Position - previous.Position)
            / Math.Max(dtSeconds, BrushPredictionPreviewDefaults.MinPredictionDtSeconds);
        return new Vector(
            (currentVelocity.X * BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor)
            + (measured.X * BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor),
            (currentVelocity.Y * BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor)
            + (measured.Y * BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor));
    }

    /// <summary>
    /// 由相邻两次平滑速度的差分估计加速度并做 EMA 平滑 + 幅值钳制，
    /// 仅用于预测预览段的二阶外推项，不进入任何已提交几何。
    /// </summary>
    internal static Vector ResolveAcceleration(
        Vector currentAcceleration,
        BrushInputSample previous,
        BrushInputSample current,
        Vector previousVelocity,
        Vector currentVelocity)
    {
        var dtMs = (current.TimestampTicks - previous.TimestampTicks)
            * 1000.0
            / Math.Max(Stopwatch.Frequency, 1);
        if (dtMs < InkInputRuntimeDefaults.PredictionUpdateMinDtMs)
        {
            return currentAcceleration;
        }

        var dtSeconds = Math.Max(dtMs / 1000.0, BrushPredictionPreviewDefaults.MinPredictionDtSeconds);
        var raw = (currentVelocity - previousVelocity) / dtSeconds;
        var smoothed = new Vector(
            (currentAcceleration.X * BrushPredictionPreviewDefaults.AccelerationKeepFactor)
            + (raw.X * BrushPredictionPreviewDefaults.AccelerationApplyFactor),
            (currentAcceleration.Y * BrushPredictionPreviewDefaults.AccelerationKeepFactor)
            + (raw.Y * BrushPredictionPreviewDefaults.AccelerationApplyFactor));

        double maxMagnitude = BrushPredictionPreviewDefaults.MaxAccelerationDipPerSecSq;
        double magnitudeSq = smoothed.LengthSquared;
        if (magnitudeSq > (maxMagnitude * maxMagnitude))
        {
            smoothed *= maxMagnitude / Math.Sqrt(magnitudeSq);
        }

        return smoothed;
    }
}

public enum WhiteboardBrushPreset
{
    Smooth = 0,
    Balanced,
    Sharp
}

public enum CalligraphyBrushPreset
{
    Sharp = 0,
    Balanced,
    Soft
}

internal static class PaintPresetDefaults
{
    internal const int WpsDebounceBalancedMs = 120;
    internal const int WpsDebounceResponsiveMs = 80;
    internal const int WpsDebounceStableMs = 200;
    internal const int WpsDebounceDualScreenMs = 160;
    internal const int WpsDebounceLegacyDefaultMs = 200;
    internal const int WpsDebounceDefaultMs = WpsDebounceBalancedMs;

    internal const int PostInputRefreshDefaultMs = 120;

    internal const int PostInputBalancedMs = 120;
    internal const int PostInputResponsiveMs = 80;
    internal const int PostInputStableMs = 140;
    internal const int PostInputDualScreenMs = 160;

    internal const double WheelZoomBalanced = 1.0008;
    internal const double WheelZoomResponsive = 1.0010;
    internal const double WheelZoomStable = 1.0006;
    internal const double WheelZoomDualScreen = 1.0007;

    internal const double GestureSensitivityResponsive = 1.2;
    internal const double GestureSensitivityStable = 0.8;
    internal const double GestureSensitivityDualScreen = 0.9;

    internal const string InertiaProfileBalanced = PhotoInertiaProfileDefaults.Standard;
    internal const string InertiaProfileResponsive = PhotoInertiaProfileDefaults.Sensitive;
    internal const string InertiaProfileStable = PhotoInertiaProfileDefaults.Heavy;
    internal const string InertiaProfileDualScreen = PhotoInertiaProfileDefaults.Heavy;
}

internal static class PresetSchemeDefaults
{
    internal const string Custom = "custom";
    internal const string Balanced = "balanced";
    internal const string Responsive = "responsive";
    internal const string Stable = "stable";
    internal const string DualScreen = "dual_screen";
}

internal readonly record struct PresetSchemeInitializationResult(
    bool ShouldPersist,
    bool AppliedRecommendation,
    string FinalScheme,
    string RecommendationReason = "",
    bool RecommendationHasAdaptiveSignal = false);

internal static class PresetSchemeInitializationPolicy
{
    internal const int CurrentVersion = 1;

    internal static PresetSchemeInitializationResult Resolve(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var currentScheme = NormalizeScheme(settings.PresetScheme);
        var schemeNormalized = !string.Equals(
            settings.PresetScheme,
            currentScheme,
            StringComparison.OrdinalIgnoreCase);
        if (schemeNormalized)
        {
            settings.PresetScheme = currentScheme;
        }

        if (settings.PresetRecommendationVersion >= CurrentVersion)
        {
            return new PresetSchemeInitializationResult(
                ShouldPersist: schemeNormalized,
                AppliedRecommendation: false,
                FinalScheme: currentScheme,
                RecommendationReason: "already_initialized");
        }

        if (!ShouldApplyRecommendation(settings, currentScheme))
        {
            settings.PresetRecommendationVersion = CurrentVersion;
            return new PresetSchemeInitializationResult(
                ShouldPersist: true,
                AppliedRecommendation: false,
                FinalScheme: currentScheme,
                RecommendationReason: "manual_or_nondefault_values");
        }

        var recommendation = PresetSchemePolicy.ResolveRecommendation(settings);
        if (!PresetSchemePolicy.TryResolveManagedParameters(recommendation.Scheme, out var parameters))
        {
            settings.PresetRecommendationVersion = CurrentVersion;
            return new PresetSchemeInitializationResult(
                ShouldPersist: true,
                AppliedRecommendation: false,
                FinalScheme: currentScheme,
                RecommendationReason: recommendation.Reason,
                RecommendationHasAdaptiveSignal: recommendation.HasAdaptiveSignal);
        }

        settings.WpsInputMode = parameters.WpsInputMode;
        settings.WpsWheelForward = parameters.WpsWheelForward;
        settings.PresentationLockStrategyWhenDegraded = parameters.LockStrategyWhenDegraded;
        settings.PresentationAutoFallbackFailureThreshold = parameters.AutoFallbackFailureThreshold;
        settings.PresentationAutoFallbackProbeIntervalCommands = parameters.AutoFallbackProbeIntervalCommands;
        settings.ClassroomWritingMode = parameters.ClassroomWritingMode;
        settings.WpsDebounceMs = parameters.WpsDebounceMs;
        settings.PhotoPostInputRefreshDelayMs = parameters.PhotoPostInputRefreshDelayMs;
        settings.PhotoWheelZoomBase = parameters.PhotoWheelZoomBase;
        settings.PhotoGestureZoomSensitivity = parameters.PhotoGestureZoomSensitivity;
        settings.PresetScheme = recommendation.Scheme;
        settings.PresetRecommendationVersion = CurrentVersion;

        return new PresetSchemeInitializationResult(
            ShouldPersist: true,
            AppliedRecommendation: true,
            FinalScheme: recommendation.Scheme,
            RecommendationReason: recommendation.Reason,
            RecommendationHasAdaptiveSignal: recommendation.HasAdaptiveSignal);
    }

    private static bool ShouldApplyRecommendation(AppSettings settings, string currentScheme)
    {
        if (currentScheme is PresetSchemeDefaults.Responsive or PresetSchemeDefaults.Stable)
        {
            return false;
        }

        bool usesCurrentWpsDefault = string.Equals(settings.WpsInputMode, WpsInputModeDefaults.Auto, StringComparison.OrdinalIgnoreCase);
        bool usesLegacyWpsDefault = string.Equals(settings.WpsInputMode, WpsInputModeDefaults.Message, StringComparison.OrdinalIgnoreCase);
        if (!usesCurrentWpsDefault && !usesLegacyWpsDefault)
        {
            return false;
        }

        if (!settings.WpsWheelForward || !settings.PresentationLockStrategyWhenDegraded)
        {
            return false;
        }

        if (settings.PresentationAutoFallbackFailureThreshold
            != ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault)
        {
            return false;
        }

        if (settings.PresentationAutoFallbackProbeIntervalCommands
            != ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault)
        {
            return false;
        }

        if (settings.ClassroomWritingMode != ClassroomWritingMode.Balanced)
        {
            return false;
        }

        if (settings.PhotoPostInputRefreshDelayMs != PaintPresetDefaults.PostInputBalancedMs)
        {
            return false;
        }

        if (!IsNear(settings.PhotoWheelZoomBase, PaintPresetDefaults.WheelZoomBalanced)
            || !IsNear(settings.PhotoGestureZoomSensitivity, PhotoZoomInputDefaults.GestureSensitivityDefault))
        {
            return false;
        }

        // Allow current default (120ms) and legacy default (200ms).
        if (settings.WpsDebounceMs != PaintPresetDefaults.WpsDebounceDefaultMs
            && settings.WpsDebounceMs != PaintPresetDefaults.WpsDebounceLegacyDefaultMs)
        {
            return false;
        }

        return currentScheme is PresetSchemeDefaults.Custom or PresetSchemeDefaults.Balanced;
    }

    private static bool IsNear(double left, double right)
    {
        return Math.Abs(left - right) < PaintSettingsDefaults.DoubleComparisonEpsilon;
    }

    private static string NormalizeScheme(string? scheme)
    {
        var normalized = (scheme ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return PresetSchemeDefaults.Custom;
        }

        if (normalized == "DUAL_SCREEN")
        {
            return PresetSchemeDefaults.Stable;
        }

        return normalized switch
        {
            "CUSTOM" => PresetSchemeDefaults.Custom,
            "BALANCED" => PresetSchemeDefaults.Balanced,
            "RESPONSIVE" => PresetSchemeDefaults.Responsive,
            "STABLE" => PresetSchemeDefaults.Stable,
            _ => PresetSchemeDefaults.Custom
        };
    }
}

internal readonly record struct PresetSchemeManagedParameters(
    string WpsInputMode,
    bool WpsWheelForward,
    bool LockStrategyWhenDegraded,
    int AutoFallbackFailureThreshold,
    int AutoFallbackProbeIntervalCommands,
    ClassroomWritingMode ClassroomWritingMode,
    int WpsDebounceMs,
    int PhotoPostInputRefreshDelayMs,
    double PhotoWheelZoomBase,
    double PhotoGestureZoomSensitivity,
    string PhotoInertiaProfile);

internal readonly record struct PresetSchemeRecommendation(
    string Scheme,
    string Reason,
    bool HasAdaptiveSignal);

internal static class PresetSchemePolicy
{
    internal static bool TryResolveManagedParameters(string preset, out PresetSchemeManagedParameters parameters)
    {
        var normalizedPreset = (preset ?? string.Empty).Trim().ToUpperInvariant();
        switch (normalizedPreset)
        {
            case "BALANCED":
                parameters = new PresetSchemeManagedParameters(
                    WpsInputModeDefaults.Auto,
                    WpsWheelForward: true,
                    LockStrategyWhenDegraded: true,
                    AutoFallbackFailureThreshold: 2,
                    AutoFallbackProbeIntervalCommands: 8,
                    ClassroomWritingMode.Balanced,
                    PaintPresetDefaults.WpsDebounceBalancedMs,
                    PaintPresetDefaults.PostInputBalancedMs,
                    PaintPresetDefaults.WheelZoomBalanced,
                    PhotoZoomInputDefaults.GestureSensitivityDefault,
                    PaintPresetDefaults.InertiaProfileBalanced);
                return true;
            case "RESPONSIVE":
                parameters = new PresetSchemeManagedParameters(
                    WpsInputModeDefaults.Auto,
                    WpsWheelForward: true,
                    LockStrategyWhenDegraded: true,
                    AutoFallbackFailureThreshold: 3,
                    AutoFallbackProbeIntervalCommands: 6,
                    ClassroomWritingMode.Responsive,
                    PaintPresetDefaults.WpsDebounceResponsiveMs,
                    PaintPresetDefaults.PostInputResponsiveMs,
                    PaintPresetDefaults.WheelZoomResponsive,
                    PaintPresetDefaults.GestureSensitivityResponsive,
                    PaintPresetDefaults.InertiaProfileResponsive);
                return true;
            case "STABLE":
                parameters = new PresetSchemeManagedParameters(
                    WpsInputModeDefaults.Message,
                    WpsWheelForward: true,
                    LockStrategyWhenDegraded: true,
                    AutoFallbackFailureThreshold: 2,
                    AutoFallbackProbeIntervalCommands: 12,
                    ClassroomWritingMode.Stable,
                    PaintPresetDefaults.WpsDebounceStableMs,
                    PaintPresetDefaults.PostInputStableMs,
                    PaintPresetDefaults.WheelZoomStable,
                    PaintPresetDefaults.GestureSensitivityStable,
                    PaintPresetDefaults.InertiaProfileStable);
                return true;
            case "DUAL_SCREEN":
                parameters = new PresetSchemeManagedParameters(
                    WpsInputModeDefaults.Message,
                    WpsWheelForward: true,
                    LockStrategyWhenDegraded: true,
                    AutoFallbackFailureThreshold: 2,
                    AutoFallbackProbeIntervalCommands: 12,
                    ClassroomWritingMode.Stable,
                    PaintPresetDefaults.WpsDebounceStableMs,
                    PaintPresetDefaults.PostInputStableMs,
                    PaintPresetDefaults.WheelZoomStable,
                    PaintPresetDefaults.GestureSensitivityStable,
                    PaintPresetDefaults.InertiaProfileStable);
                return true;
            default:
                parameters = default;
                return false;
        }
    }

    internal static string ResolveInitialScheme(AppSettings settings)
    {
        var configured = (settings.PresetScheme ?? string.Empty).Trim();
        if (IsKnownScheme(configured))
        {
            var canonicalConfigured = NormalizeLegacyScheme(configured);
            if (canonicalConfigured == PresetSchemeDefaults.Custom || Matches(settings, configured))
            {
                return canonicalConfigured;
            }

            if (TryInferByParameters(settings, out var inferred))
            {
                return NormalizeLegacyScheme(inferred);
            }

            return PresetSchemeDefaults.Custom;
        }

        if (TryInferByParameters(settings, out var fallbackInferred))
        {
            return NormalizeLegacyScheme(fallbackInferred);
        }

        return PresetSchemeDefaults.Custom;
    }

    internal static PresetSchemeRecommendation ResolveRecommendation(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var pressureProfile = ResolvePressureProfile(settings.StylusAdaptivePressureProfile);
        var sampleRateTier = ResolveSampleRateTier(settings.StylusAdaptiveSampleRateTier);
        var profileSummary = BuildProfileSummary(pressureProfile, sampleRateTier);
        var hasAdaptiveSignal = pressureProfile != StylusPressureDeviceProfile.Unknown
            || sampleRateTier != StylusSampleRateTier.Unknown;

        if (!hasAdaptiveSignal)
        {
            return new PresetSchemeRecommendation(
                PresetSchemeDefaults.Balanced,
                string.Empty,
                HasAdaptiveSignal: false);
        }

        bool lowPressureReliability = pressureProfile is StylusPressureDeviceProfile.EndpointPseudo or StylusPressureDeviceProfile.LowRange;
        bool lowSampleRate = sampleRateTier == StylusSampleRateTier.Low;
        if (lowPressureReliability || lowSampleRate)
        {
            return new PresetSchemeRecommendation(
                PresetSchemeDefaults.Stable,
                $"设备画像：{profileSummary}，建议使用高稳定以提升容错。",
                HasAdaptiveSignal: true);
        }

        if (pressureProfile == StylusPressureDeviceProfile.Continuous && sampleRateTier == StylusSampleRateTier.High)
        {
            return new PresetSchemeRecommendation(
                PresetSchemeDefaults.Responsive,
                $"设备画像：{profileSummary}，建议使用高灵敏以提高跟手性。",
                HasAdaptiveSignal: true);
        }

        if (pressureProfile == StylusPressureDeviceProfile.Continuous && sampleRateTier == StylusSampleRateTier.Medium)
        {
            return new PresetSchemeRecommendation(
                PresetSchemeDefaults.Responsive,
                $"设备画像：{profileSummary}，建议使用高灵敏（流畅优先）。",
                HasAdaptiveSignal: true);
        }

        return new PresetSchemeRecommendation(
            PresetSchemeDefaults.Balanced,
            $"设备画像：{profileSummary}，建议使用课堂平衡。",
            HasAdaptiveSignal: true);
    }

    private static bool TryInferByParameters(AppSettings settings, out string scheme)
    {
        if (Matches(settings, PresetSchemeDefaults.Balanced))
        {
            scheme = PresetSchemeDefaults.Balanced;
            return true;
        }

        if (Matches(settings, PresetSchemeDefaults.Responsive))
        {
            scheme = PresetSchemeDefaults.Responsive;
            return true;
        }

        if (Matches(settings, PresetSchemeDefaults.Stable))
        {
            scheme = PresetSchemeDefaults.Stable;
            return true;
        }

        if (Matches(settings, PresetSchemeDefaults.DualScreen))
        {
            scheme = PresetSchemeDefaults.Stable;
            return true;
        }

        if (MatchesLegacyDualScreenParameters(settings))
        {
            scheme = PresetSchemeDefaults.Stable;
            return true;
        }

        scheme = PresetSchemeDefaults.Custom;
        return false;
    }

    private static bool Matches(AppSettings settings, string scheme)
    {
        if (!TryResolveManagedParameters(scheme, out var parameters))
        {
            return false;
        }

        return string.Equals(settings.WpsInputMode, parameters.WpsInputMode, StringComparison.OrdinalIgnoreCase)
            && settings.WpsWheelForward == parameters.WpsWheelForward
            && settings.PresentationLockStrategyWhenDegraded == parameters.LockStrategyWhenDegraded
            && settings.PresentationAutoFallbackFailureThreshold == parameters.AutoFallbackFailureThreshold
            && settings.PresentationAutoFallbackProbeIntervalCommands == parameters.AutoFallbackProbeIntervalCommands
            && settings.ClassroomWritingMode == parameters.ClassroomWritingMode
            && settings.WpsDebounceMs == parameters.WpsDebounceMs
            && settings.PhotoPostInputRefreshDelayMs == parameters.PhotoPostInputRefreshDelayMs
            && Math.Abs(settings.PhotoWheelZoomBase - parameters.PhotoWheelZoomBase) < PaintSettingsDefaults.DoubleComparisonEpsilon
            && Math.Abs(settings.PhotoGestureZoomSensitivity - parameters.PhotoGestureZoomSensitivity) < PaintSettingsDefaults.DoubleComparisonEpsilon
            && string.Equals(
                PhotoInertiaProfileDefaults.Normalize(settings.PhotoInertiaProfile),
                parameters.PhotoInertiaProfile,
                StringComparison.OrdinalIgnoreCase);
    }

    private static StylusPressureDeviceProfile ResolvePressureProfile(int rawProfile)
    {
        if (Enum.IsDefined(typeof(StylusPressureDeviceProfile), rawProfile))
        {
            return (StylusPressureDeviceProfile)rawProfile;
        }

        return StylusPressureDeviceProfile.Unknown;
    }

    private static StylusSampleRateTier ResolveSampleRateTier(int rawTier)
    {
        if (Enum.IsDefined(typeof(StylusSampleRateTier), rawTier))
        {
            return (StylusSampleRateTier)rawTier;
        }

        return StylusSampleRateTier.Unknown;
    }

    private static string BuildProfileSummary(
        StylusPressureDeviceProfile pressureProfile,
        StylusSampleRateTier sampleRateTier)
    {
        var parts = new List<string>(2);
        if (pressureProfile != StylusPressureDeviceProfile.Unknown)
        {
            parts.Add(ResolvePressureLabel(pressureProfile));
        }

        if (sampleRateTier != StylusSampleRateTier.Unknown)
        {
            parts.Add(ResolveSampleRateLabel(sampleRateTier));
        }

        return parts.Count == 0
            ? "未识别"
            : string.Join(" + ", parts);
    }

    private static string ResolvePressureLabel(StylusPressureDeviceProfile profile)
    {
        return profile switch
        {
            StylusPressureDeviceProfile.Continuous => "连续压感",
            StylusPressureDeviceProfile.LowRange => "低动态压感",
            StylusPressureDeviceProfile.EndpointPseudo => "端点伪压感",
            _ => "未知压感"
        };
    }

    private static string ResolveSampleRateLabel(StylusSampleRateTier tier)
    {
        return tier switch
        {
            StylusSampleRateTier.High => "高采样率",
            StylusSampleRateTier.Medium => "中采样率",
            StylusSampleRateTier.Low => "低采样率",
            _ => "未知采样率"
        };
    }

    private static bool IsKnownScheme(string scheme)
    {
        return string.Equals(scheme, PresetSchemeDefaults.Custom, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, PresetSchemeDefaults.Balanced, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, PresetSchemeDefaults.Responsive, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, PresetSchemeDefaults.Stable, StringComparison.OrdinalIgnoreCase)
            || string.Equals(scheme, PresetSchemeDefaults.DualScreen, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeLegacyScheme(string? scheme)
    {
        var normalized = (scheme ?? string.Empty).Trim().ToUpperInvariant();
        return normalized switch
        {
            "DUAL_SCREEN" => PresetSchemeDefaults.Stable,
            "CUSTOM" => PresetSchemeDefaults.Custom,
            "BALANCED" => PresetSchemeDefaults.Balanced,
            "RESPONSIVE" => PresetSchemeDefaults.Responsive,
            "STABLE" => PresetSchemeDefaults.Stable,
            _ => PresetSchemeDefaults.Custom
        };
    }

    private static bool MatchesLegacyDualScreenParameters(AppSettings settings)
    {
        return string.Equals(settings.WpsInputMode, WpsInputModeDefaults.Message, StringComparison.OrdinalIgnoreCase)
            && settings.WpsWheelForward
            && settings.PresentationLockStrategyWhenDegraded
            && settings.PresentationAutoFallbackFailureThreshold == 2
            && settings.PresentationAutoFallbackProbeIntervalCommands == 16
            && settings.ClassroomWritingMode == ClassroomWritingMode.Stable
            && settings.WpsDebounceMs == PaintPresetDefaults.WpsDebounceDualScreenMs
            && settings.PhotoPostInputRefreshDelayMs == PaintPresetDefaults.PostInputDualScreenMs
            && Math.Abs(settings.PhotoWheelZoomBase - PaintPresetDefaults.WheelZoomDualScreen) < PaintSettingsDefaults.DoubleComparisonEpsilon
            && Math.Abs(settings.PhotoGestureZoomSensitivity - PaintPresetDefaults.GestureSensitivityDualScreen) < PaintSettingsDefaults.DoubleComparisonEpsilon
            && string.Equals(
                PhotoInertiaProfileDefaults.Normalize(settings.PhotoInertiaProfile),
                PaintPresetDefaults.InertiaProfileDualScreen,
                StringComparison.OrdinalIgnoreCase);
    }
}
