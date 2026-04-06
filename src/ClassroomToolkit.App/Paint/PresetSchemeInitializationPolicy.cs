using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct PresetSchemeInitializationResult(
    bool ShouldPersist,
    bool AppliedRecommendation,
    string FinalScheme,
    string RecommendationReason = "",
    bool RecommendationHasAdaptiveSignal = false);

internal static class PresetSchemeInitializationPolicy
{
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

        if (settings.PresetRecommendationInitialized)
        {
            return new PresetSchemeInitializationResult(
                ShouldPersist: schemeNormalized,
                AppliedRecommendation: false,
                FinalScheme: currentScheme,
                RecommendationReason: "already_initialized");
        }

        if (!ShouldApplyRecommendation(settings, currentScheme))
        {
            settings.PresetRecommendationInitialized = true;
            return new PresetSchemeInitializationResult(
                ShouldPersist: true,
                AppliedRecommendation: false,
                FinalScheme: currentScheme,
                RecommendationReason: "manual_or_nondefault_values");
        }

        var recommendation = PresetSchemePolicy.ResolveRecommendation(settings);
        if (!PresetSchemePolicy.TryResolveManagedParameters(recommendation.Scheme, out var parameters))
        {
            settings.PresetRecommendationInitialized = true;
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
        settings.PresetRecommendationInitialized = true;

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

        if (!string.Equals(settings.WpsInputMode, WpsInputModeDefaults.Message, StringComparison.OrdinalIgnoreCase))
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

        // Allow legacy default (200ms) and current balanced default (120ms).
        if (settings.WpsDebounceMs != PaintPresetDefaults.WpsDebounceDefaultMs
            && settings.WpsDebounceMs != PaintPresetDefaults.WpsDebounceBalancedMs)
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
        var normalized = (scheme ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return PresetSchemeDefaults.Custom;
        }

        if (normalized == PresetSchemeDefaults.DualScreen)
        {
            return PresetSchemeDefaults.Stable;
        }

        return normalized is PresetSchemeDefaults.Custom
            or PresetSchemeDefaults.Balanced
            or PresetSchemeDefaults.Responsive
            or PresetSchemeDefaults.Stable
            ? normalized
            : PresetSchemeDefaults.Custom;
    }
}
