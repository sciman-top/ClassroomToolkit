using System.Collections.Generic;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

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
