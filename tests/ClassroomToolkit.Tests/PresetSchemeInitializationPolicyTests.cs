using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresetSchemeInitializationPolicyTests
{
    [Fact]
    public void Resolve_ShouldNormalizePresetScheme_WhenAlreadyInitialized()
    {
        var settings = new AppSettings
        {
            PresetScheme = "LEGACY",
            PresetRecommendationInitialized = true
        };

        var result = PresetSchemeInitializationPolicy.Resolve(settings);

        result.ShouldPersist.Should().BeTrue();
        result.AppliedRecommendation.Should().BeFalse();
        result.FinalScheme.Should().Be(PresetSchemeDefaults.Custom);
        result.RecommendationReason.Should().Be("already_initialized");
        settings.PresetScheme.Should().Be(PresetSchemeDefaults.Custom);
    }

    [Fact]
    public void Resolve_ShouldApplyRecommendation_WhenDefaultsEligible()
    {
        var settings = new AppSettings
        {
            PresetScheme = PresetSchemeDefaults.Custom,
            PresetRecommendationInitialized = false,
            WpsInputMode = WpsInputModeDefaults.Auto,
            WpsWheelForward = true,
            PresentationLockStrategyWhenDegraded = true,
            ClassroomWritingMode = ClassroomWritingMode.Balanced,
            WpsDebounceMs = PaintPresetDefaults.WpsDebounceBalancedMs,
            PhotoPostInputRefreshDelayMs = PaintPresetDefaults.PostInputBalancedMs,
            PhotoWheelZoomBase = PaintPresetDefaults.WheelZoomBalanced,
            PhotoGestureZoomSensitivity = PhotoZoomInputDefaults.GestureSensitivityDefault,
            StylusAdaptivePressureProfile = (int)StylusPressureDeviceProfile.Unknown,
            StylusAdaptiveSampleRateTier = (int)StylusSampleRateTier.Low
        };

        var result = PresetSchemeInitializationPolicy.Resolve(settings);

        result.ShouldPersist.Should().BeTrue();
        result.AppliedRecommendation.Should().BeTrue();
        result.FinalScheme.Should().Be(PresetSchemeDefaults.Stable);
        result.RecommendationReason.Should().NotBeNullOrWhiteSpace();
        result.RecommendationHasAdaptiveSignal.Should().BeTrue();
        settings.PresetScheme.Should().Be(PresetSchemeDefaults.Stable);
        settings.WpsInputMode.Should().Be(WpsInputModeDefaults.Message);
        settings.PresetRecommendationInitialized.Should().BeTrue();
        settings.PresentationAutoFallbackFailureThreshold.Should().Be(2);
        settings.PresentationAutoFallbackProbeIntervalCommands.Should().Be(12);
    }

    [Fact]
    public void Resolve_ShouldOnlyMarkInitialized_WhenManagedValuesWereManuallyChanged()
    {
        var settings = new AppSettings
        {
            PresetScheme = PresetSchemeDefaults.Custom,
            PresetRecommendationInitialized = false,
            WpsInputMode = WpsInputModeDefaults.Raw
        };

        var result = PresetSchemeInitializationPolicy.Resolve(settings);

        result.ShouldPersist.Should().BeTrue();
        result.AppliedRecommendation.Should().BeFalse();
        result.FinalScheme.Should().Be(PresetSchemeDefaults.Custom);
        result.RecommendationReason.Should().Be("manual_or_nondefault_values");
        settings.PresetRecommendationInitialized.Should().BeTrue();
    }
}
