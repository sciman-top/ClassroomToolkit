using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Settings;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PresetSchemePolicyTests
{
    [Theory]
    [InlineData(PresetSchemeDefaults.Balanced, (int)ClassroomWritingMode.Balanced, 120, 120, 1.0008, 1.0)]
    [InlineData(PresetSchemeDefaults.Responsive, (int)ClassroomWritingMode.Responsive, 80, 80, 1.0010, 1.2)]
    [InlineData(PresetSchemeDefaults.Stable, (int)ClassroomWritingMode.Stable, 200, 140, 1.0006, 0.8)]
    [InlineData(PresetSchemeDefaults.DualScreen, (int)ClassroomWritingMode.Stable, 160, 160, 1.0007, 0.9)]
    public void TryResolveManagedParameters_ShouldReturnExpectedValues(
        string scheme,
        int writingMode,
        int wpsDebounceMs,
        int postInputDelayMs,
        double wheelZoomBase,
        double gestureSensitivity)
    {
        var resolved = PresetSchemePolicy.TryResolveManagedParameters(scheme, out var parameters);

        resolved.Should().BeTrue();
        parameters.WpsInputMode.Should().Be(WpsInputModeDefaults.Message);
        parameters.WpsWheelForward.Should().BeTrue();
        parameters.LockStrategyWhenDegraded.Should().BeTrue();
        parameters.ClassroomWritingMode.Should().Be((ClassroomWritingMode)writingMode);
        parameters.WpsDebounceMs.Should().Be(wpsDebounceMs);
        parameters.PhotoPostInputRefreshDelayMs.Should().Be(postInputDelayMs);
        parameters.PhotoWheelZoomBase.Should().BeApproximately(wheelZoomBase, 1e-9);
        parameters.PhotoGestureZoomSensitivity.Should().BeApproximately(gestureSensitivity, 1e-9);
    }

    [Fact]
    public void TryResolveManagedParameters_ShouldReturnFalse_ForCustom()
    {
        var resolved = PresetSchemePolicy.TryResolveManagedParameters(PresetSchemeDefaults.Custom, out _);

        resolved.Should().BeFalse();
    }

    [Fact]
    public void ResolveInitialScheme_ShouldUseConfiguredScheme_WhenKnownAndMatching()
    {
        var settings = new AppSettings
        {
            PresetScheme = PresetSchemeDefaults.Responsive,
            ClassroomWritingMode = ClassroomWritingMode.Responsive,
            WpsDebounceMs = PaintPresetDefaults.WpsDebounceResponsiveMs,
            PhotoPostInputRefreshDelayMs = PaintPresetDefaults.PostInputResponsiveMs,
            PhotoWheelZoomBase = PaintPresetDefaults.WheelZoomResponsive,
            PhotoGestureZoomSensitivity = PaintPresetDefaults.GestureSensitivityResponsive
        };

        var scheme = PresetSchemePolicy.ResolveInitialScheme(settings);

        scheme.Should().Be(PresetSchemeDefaults.Responsive);
    }

    [Fact]
    public void ResolveInitialScheme_ShouldInferByParameters_WhenConfiguredSchemeMismatched()
    {
        var settings = new AppSettings
        {
            PresetScheme = PresetSchemeDefaults.Responsive,
            ClassroomWritingMode = ClassroomWritingMode.Stable,
            WpsDebounceMs = PaintPresetDefaults.WpsDebounceDualScreenMs,
            PhotoPostInputRefreshDelayMs = PaintPresetDefaults.PostInputDualScreenMs,
            PhotoWheelZoomBase = PaintPresetDefaults.WheelZoomDualScreen,
            PhotoGestureZoomSensitivity = PaintPresetDefaults.GestureSensitivityDualScreen
        };

        var scheme = PresetSchemePolicy.ResolveInitialScheme(settings);

        scheme.Should().Be(PresetSchemeDefaults.DualScreen);
    }

    [Fact]
    public void ResolveInitialScheme_ShouldInferScheme_WhenConfiguredUnknown()
    {
        var settings = new AppSettings
        {
            PresetScheme = "legacy",
            ClassroomWritingMode = ClassroomWritingMode.Stable,
            WpsDebounceMs = PaintPresetDefaults.WpsDebounceDualScreenMs,
            PhotoPostInputRefreshDelayMs = PaintPresetDefaults.PostInputDualScreenMs,
            PhotoWheelZoomBase = PaintPresetDefaults.WheelZoomDualScreen,
            PhotoGestureZoomSensitivity = PaintPresetDefaults.GestureSensitivityDualScreen
        };

        var scheme = PresetSchemePolicy.ResolveInitialScheme(settings);

        scheme.Should().Be(PresetSchemeDefaults.DualScreen);
    }

    [Fact]
    public void ResolveInitialScheme_ShouldFallbackToCustom_WhenParametersDoNotMatchAnyPreset()
    {
        var settings = new AppSettings
        {
            PresetScheme = "legacy",
            ClassroomWritingMode = ClassroomWritingMode.Balanced,
            WpsDebounceMs = PaintPresetDefaults.WpsDebounceBalancedMs,
            PhotoPostInputRefreshDelayMs = PaintPresetDefaults.PostInputBalancedMs,
            PhotoWheelZoomBase = PaintPresetDefaults.WheelZoomBalanced,
            PhotoGestureZoomSensitivity = PaintPresetDefaults.GestureSensitivityResponsive
        };

        var scheme = PresetSchemePolicy.ResolveInitialScheme(settings);

        scheme.Should().Be(PresetSchemeDefaults.Custom);
    }

    [Fact]
    public void ResolveInitialScheme_ShouldFallbackToCustom_WhenManagedToggleDiffers()
    {
        var settings = new AppSettings
        {
            PresetScheme = PresetSchemeDefaults.Balanced,
            ClassroomWritingMode = ClassroomWritingMode.Balanced,
            WpsDebounceMs = PaintPresetDefaults.WpsDebounceBalancedMs,
            PhotoPostInputRefreshDelayMs = PaintPresetDefaults.PostInputBalancedMs,
            PhotoWheelZoomBase = PaintPresetDefaults.WheelZoomBalanced,
            PhotoGestureZoomSensitivity = PhotoZoomInputDefaults.GestureSensitivityDefault,
            WpsInputMode = WpsInputModeDefaults.Auto
        };

        var scheme = PresetSchemePolicy.ResolveInitialScheme(settings);

        scheme.Should().Be(PresetSchemeDefaults.Custom);
    }

    [Fact]
    public void ResolveRecommendation_ShouldFallbackToBalanced_WhenAdaptiveSignalMissing()
    {
        var settings = new AppSettings
        {
            StylusAdaptivePressureProfile = 0,
            StylusAdaptiveSampleRateTier = 0
        };

        var recommendation = PresetSchemePolicy.ResolveRecommendation(settings);

        recommendation.Scheme.Should().Be(PresetSchemeDefaults.Balanced);
        recommendation.HasAdaptiveSignal.Should().BeFalse();
    }

    [Fact]
    public void ResolveRecommendation_ShouldRecommendStable_WhenSampleRateIsLow()
    {
        var settings = new AppSettings
        {
            StylusAdaptivePressureProfile = 0,
            StylusAdaptiveSampleRateTier = 1
        };

        var recommendation = PresetSchemePolicy.ResolveRecommendation(settings);

        recommendation.Scheme.Should().Be(PresetSchemeDefaults.Stable);
        recommendation.HasAdaptiveSignal.Should().BeTrue();
    }

    [Fact]
    public void ResolveRecommendation_ShouldKeepDualScreen_WhenCurrentSchemeIsDualScreenAndLowRate()
    {
        var settings = new AppSettings
        {
            PresetScheme = PresetSchemeDefaults.DualScreen,
            StylusAdaptivePressureProfile = 0,
            StylusAdaptiveSampleRateTier = 1
        };

        var recommendation = PresetSchemePolicy.ResolveRecommendation(settings);

        recommendation.Scheme.Should().Be(PresetSchemeDefaults.DualScreen);
        recommendation.HasAdaptiveSignal.Should().BeTrue();
    }

    [Fact]
    public void ResolveRecommendation_ShouldRecommendResponsive_WhenPressureContinuousAndRateHigh()
    {
        var settings = new AppSettings
        {
            StylusAdaptivePressureProfile = 1,
            StylusAdaptiveSampleRateTier = 3
        };

        var recommendation = PresetSchemePolicy.ResolveRecommendation(settings);

        recommendation.Scheme.Should().Be(PresetSchemeDefaults.Responsive);
        recommendation.HasAdaptiveSignal.Should().BeTrue();
    }

    [Fact]
    public void ResolveRecommendation_ShouldRecommendBalanced_WhenSignalsDoNotMatchOtherProfiles()
    {
        var settings = new AppSettings
        {
            StylusAdaptivePressureProfile = 1,
            StylusAdaptiveSampleRateTier = 0
        };

        var recommendation = PresetSchemePolicy.ResolveRecommendation(settings);

        recommendation.Scheme.Should().Be(PresetSchemeDefaults.Balanced);
        recommendation.HasAdaptiveSignal.Should().BeTrue();
    }
}
