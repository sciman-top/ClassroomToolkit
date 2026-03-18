using System.Collections.Generic;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct PresetSchemeManagedParameters(
    string WpsInputMode,
    bool WpsWheelForward,
    bool LockStrategyWhenDegraded,
    ClassroomWritingMode ClassroomWritingMode,
    int WpsDebounceMs,
    int PhotoPostInputRefreshDelayMs,
    double PhotoWheelZoomBase,
    double PhotoGestureZoomSensitivity);

internal readonly record struct PresetSchemeRecommendation(
    string Scheme,
    string Reason,
    bool HasAdaptiveSignal);

internal static class PresetSchemePolicy
{
    internal static bool TryResolveManagedParameters(string preset, out PresetSchemeManagedParameters parameters)
    {
        var normalizedPreset = (preset ?? string.Empty).Trim().ToLowerInvariant();
        switch (normalizedPreset)
        {
            case PresetSchemeDefaults.Balanced:
                parameters = new PresetSchemeManagedParameters(
                    WpsInputModeDefaults.Message,
                    WpsWheelForward: true,
                    LockStrategyWhenDegraded: true,
                    ClassroomWritingMode.Balanced,
                    PaintPresetDefaults.WpsDebounceBalancedMs,
                    PaintPresetDefaults.PostInputBalancedMs,
                    PaintPresetDefaults.WheelZoomBalanced,
                    PhotoZoomInputDefaults.GestureSensitivityDefault);
                return true;
            case PresetSchemeDefaults.Responsive:
                parameters = new PresetSchemeManagedParameters(
                    WpsInputModeDefaults.Message,
                    WpsWheelForward: true,
                    LockStrategyWhenDegraded: true,
                    ClassroomWritingMode.Responsive,
                    PaintPresetDefaults.WpsDebounceResponsiveMs,
                    PaintPresetDefaults.PostInputResponsiveMs,
                    PaintPresetDefaults.WheelZoomResponsive,
                    PaintPresetDefaults.GestureSensitivityResponsive);
                return true;
            case PresetSchemeDefaults.Stable:
                parameters = new PresetSchemeManagedParameters(
                    WpsInputModeDefaults.Message,
                    WpsWheelForward: true,
                    LockStrategyWhenDegraded: true,
                    ClassroomWritingMode.Stable,
                    PaintPresetDefaults.WpsDebounceStableMs,
                    PaintPresetDefaults.PostInputStableMs,
                    PaintPresetDefaults.WheelZoomStable,
                    PaintPresetDefaults.GestureSensitivityStable);
                return true;
            case PresetSchemeDefaults.DualScreen:
                parameters = new PresetSchemeManagedParameters(
                    WpsInputModeDefaults.Message,
                    WpsWheelForward: true,
                    LockStrategyWhenDegraded: true,
                    ClassroomWritingMode.Stable,
                    PaintPresetDefaults.WpsDebounceDualScreenMs,
                    PaintPresetDefaults.PostInputDualScreenMs,
                    PaintPresetDefaults.WheelZoomDualScreen,
                    PaintPresetDefaults.GestureSensitivityDualScreen);
                return true;
            default:
                parameters = default;
                return false;
        }
    }

    internal static string ResolveInitialScheme(AppSettings settings)
    {
        var configured = (settings.PresetScheme ?? string.Empty).Trim().ToLowerInvariant();
        if (IsKnownScheme(configured))
        {
            if (configured == PresetSchemeDefaults.Custom || Matches(settings, configured))
            {
                return configured;
            }

            if (TryInferByParameters(settings, out var inferred))
            {
                return inferred;
            }

            return PresetSchemeDefaults.Custom;
        }

        if (TryInferByParameters(settings, out var fallbackInferred))
        {
            return fallbackInferred;
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
                "尚未采集到足够书写样本，建议先用课堂平衡（推荐）。",
                HasAdaptiveSignal: false);
        }

        bool lowPressureReliability = pressureProfile is StylusPressureDeviceProfile.EndpointPseudo or StylusPressureDeviceProfile.LowRange;
        bool lowSampleRate = sampleRateTier == StylusSampleRateTier.Low;
        if (lowPressureReliability || lowSampleRate)
        {
            bool keepDualScreen = lowSampleRate
                && string.Equals(
                    (settings.PresetScheme ?? string.Empty).Trim(),
                    PresetSchemeDefaults.DualScreen,
                    StringComparison.OrdinalIgnoreCase);
            var recommended = keepDualScreen ? PresetSchemeDefaults.DualScreen : PresetSchemeDefaults.Stable;
            return new PresetSchemeRecommendation(
                recommended,
                $"设备画像：{profileSummary}，建议使用{(keepDualScreen ? "双屏投影" : "高稳定")}以提升容错。",
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
            scheme = PresetSchemeDefaults.DualScreen;
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
            && settings.ClassroomWritingMode == parameters.ClassroomWritingMode
            && settings.WpsDebounceMs == parameters.WpsDebounceMs
            && settings.PhotoPostInputRefreshDelayMs == parameters.PhotoPostInputRefreshDelayMs
            && Math.Abs(settings.PhotoWheelZoomBase - parameters.PhotoWheelZoomBase) < PaintSettingsDefaults.DoubleComparisonEpsilon
            && Math.Abs(settings.PhotoGestureZoomSensitivity - parameters.PhotoGestureZoomSensitivity) < PaintSettingsDefaults.DoubleComparisonEpsilon;
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
        return scheme is PresetSchemeDefaults.Custom
            or PresetSchemeDefaults.Balanced
            or PresetSchemeDefaults.Responsive
            or PresetSchemeDefaults.Stable
            or PresetSchemeDefaults.DualScreen;
    }
}
