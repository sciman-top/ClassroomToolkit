using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ClassroomWritingModeTunerTests
{
    [Fact]
    public void ResolveRuntimeSettings_ShouldReturnBalancedDefaults()
    {
        var runtime = ClassroomWritingModeTuner.ResolveRuntimeSettings(ClassroomWritingMode.Balanced);

        runtime.PseudoPressureLowThreshold.Should().BeApproximately(ClassroomWritingModeTuner.DefaultPseudoPressureLowThreshold, 1e-9);
        runtime.PseudoPressureHighThreshold.Should().BeApproximately(ClassroomWritingModeTuner.DefaultPseudoPressureHighThreshold, 1e-9);
        runtime.CalligraphyPreviewMinDistance.Should().BeApproximately(ClassroomWritingModeTuner.DefaultCalligraphyPreviewMinDistance, 1e-9);
    }

    [Fact]
    public void ResolveRuntimeSettings_ShouldFollowExpectedOrderingAcrossModes()
    {
        var stable = ClassroomWritingModeTuner.ResolveRuntimeSettings(ClassroomWritingMode.Stable);
        var balanced = ClassroomWritingModeTuner.ResolveRuntimeSettings(ClassroomWritingMode.Balanced);
        var responsive = ClassroomWritingModeTuner.ResolveRuntimeSettings(ClassroomWritingMode.Responsive);

        stable.PseudoPressureLowThreshold.Should().BeGreaterThan(balanced.PseudoPressureLowThreshold);
        balanced.PseudoPressureLowThreshold.Should().BeGreaterThan(responsive.PseudoPressureLowThreshold);

        stable.PseudoPressureHighThreshold.Should().BeLessThan(balanced.PseudoPressureHighThreshold);
        balanced.PseudoPressureHighThreshold.Should().BeLessThan(responsive.PseudoPressureHighThreshold);

        stable.CalligraphyPreviewMinDistance.Should().BeGreaterThan(balanced.CalligraphyPreviewMinDistance);
        balanced.CalligraphyPreviewMinDistance.Should().BeGreaterThan(responsive.CalligraphyPreviewMinDistance);
    }

    [Fact]
    public void ApplyToMarkerConfig_ShouldScalePressureWidthFactorByMode()
    {
        var stable = MarkerBrushConfig.Balanced;
        var balanced = MarkerBrushConfig.Balanced;
        var responsive = MarkerBrushConfig.Balanced;

        ClassroomWritingModeTuner.ApplyToMarkerConfig(stable, ClassroomWritingMode.Stable);
        ClassroomWritingModeTuner.ApplyToMarkerConfig(balanced, ClassroomWritingMode.Balanced);
        ClassroomWritingModeTuner.ApplyToMarkerConfig(responsive, ClassroomWritingMode.Responsive);

        stable.PressureWidthFactor.Should().BeLessThan(balanced.PressureWidthFactor);
        balanced.PressureWidthFactor.Should().BeLessThan(responsive.PressureWidthFactor);
    }

    [Fact]
    public void ApplyToMarkerConfig_ShouldClampPressureWidthFactorToSafeRange()
    {
        var low = new MarkerBrushConfig { PressureWidthFactor = 0.001 };
        var high = new MarkerBrushConfig { PressureWidthFactor = 10.0 };

        ClassroomWritingModeTuner.ApplyToMarkerConfig(low, ClassroomWritingMode.Stable);
        ClassroomWritingModeTuner.ApplyToMarkerConfig(high, ClassroomWritingMode.Responsive);

        low.PressureWidthFactor.Should().Be(0.02);
        high.PressureWidthFactor.Should().Be(0.45);
    }

    [Fact]
    public void ApplyToCalligraphyConfig_ShouldScalePressureSettingsByMode()
    {
        var stable = BrushPhysicsConfig.CreateCalligraphyBalanced();
        var balanced = BrushPhysicsConfig.CreateCalligraphyBalanced();
        var responsive = BrushPhysicsConfig.CreateCalligraphyBalanced();

        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(stable, ClassroomWritingMode.Stable);
        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(balanced, ClassroomWritingMode.Balanced);
        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(responsive, ClassroomWritingMode.Responsive);

        stable.RealPressureWidthInfluence.Should().BeLessThan(balanced.RealPressureWidthInfluence);
        balanced.RealPressureWidthInfluence.Should().BeLessThan(responsive.RealPressureWidthInfluence);

        stable.RealPressureWidthScale.Should().BeLessThan(balanced.RealPressureWidthScale);
        balanced.RealPressureWidthScale.Should().BeLessThan(responsive.RealPressureWidthScale);
    }

    [Fact]
    public void ApplyToCalligraphyConfig_ShouldClampPressureSettingsToSafeRange()
    {
        var low = new BrushPhysicsConfig
        {
            RealPressureWidthInfluence = 0.01,
            RealPressureWidthScale = 0.01
        };
        var high = new BrushPhysicsConfig
        {
            RealPressureWidthInfluence = 10.0,
            RealPressureWidthScale = 10.0
        };

        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(low, ClassroomWritingMode.Stable);
        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(high, ClassroomWritingMode.Responsive);

        low.RealPressureWidthInfluence.Should().Be(0.2);
        low.RealPressureWidthScale.Should().Be(0.12);
        high.RealPressureWidthInfluence.Should().Be(0.9);
        high.RealPressureWidthScale.Should().Be(0.55);
    }

    [Fact]
    public void TryResolveStylusPressure_ShouldRejectPseudoPressureEndpoints()
    {
        var runtime = ClassroomWritingModeTuner.ResolveRuntimeSettings(ClassroomWritingMode.Balanced);

        var lowAccepted = ClassroomWritingModeTuner.TryResolveStylusPressure(
            runtime.PseudoPressureLowThreshold,
            runtime.PseudoPressureLowThreshold,
            runtime.PseudoPressureHighThreshold,
            out _);
        var highAccepted = ClassroomWritingModeTuner.TryResolveStylusPressure(
            runtime.PseudoPressureHighThreshold,
            runtime.PseudoPressureLowThreshold,
            runtime.PseudoPressureHighThreshold,
            out _);
        var minAccepted = ClassroomWritingModeTuner.TryResolveStylusPressure(
            0.0,
            runtime.PseudoPressureLowThreshold,
            runtime.PseudoPressureHighThreshold,
            out _);
        var maxAccepted = ClassroomWritingModeTuner.TryResolveStylusPressure(
            1.0,
            runtime.PseudoPressureLowThreshold,
            runtime.PseudoPressureHighThreshold,
            out _);

        lowAccepted.Should().BeFalse();
        highAccepted.Should().BeFalse();
        minAccepted.Should().BeFalse();
        maxAccepted.Should().BeFalse();
    }

    [Fact]
    public void TryResolveStylusPressure_ShouldApplyModeSpecificThresholdSegmentation()
    {
        const double nearLow = 0.00015;
        const double nearHigh = 0.99985;

        var stable = ClassroomWritingModeTuner.ResolveRuntimeSettings(ClassroomWritingMode.Stable);
        var balanced = ClassroomWritingModeTuner.ResolveRuntimeSettings(ClassroomWritingMode.Balanced);
        var responsive = ClassroomWritingModeTuner.ResolveRuntimeSettings(ClassroomWritingMode.Responsive);

        ClassroomWritingModeTuner.TryResolveStylusPressure(
            nearLow,
            stable.PseudoPressureLowThreshold,
            stable.PseudoPressureHighThreshold,
            out _).Should().BeFalse();
        ClassroomWritingModeTuner.TryResolveStylusPressure(
            nearLow,
            balanced.PseudoPressureLowThreshold,
            balanced.PseudoPressureHighThreshold,
            out _).Should().BeTrue();
        ClassroomWritingModeTuner.TryResolveStylusPressure(
            nearLow,
            responsive.PseudoPressureLowThreshold,
            responsive.PseudoPressureHighThreshold,
            out _).Should().BeTrue();

        ClassroomWritingModeTuner.TryResolveStylusPressure(
            nearHigh,
            stable.PseudoPressureLowThreshold,
            stable.PseudoPressureHighThreshold,
            out _).Should().BeFalse();
        ClassroomWritingModeTuner.TryResolveStylusPressure(
            nearHigh,
            balanced.PseudoPressureLowThreshold,
            balanced.PseudoPressureHighThreshold,
            out _).Should().BeTrue();
        ClassroomWritingModeTuner.TryResolveStylusPressure(
            nearHigh,
            responsive.PseudoPressureLowThreshold,
            responsive.PseudoPressureHighThreshold,
            out _).Should().BeTrue();
    }

    [Fact]
    public void TryResolveStylusPressure_ShouldRejectInvalidNumericInput()
    {
        var acceptedNaN = ClassroomWritingModeTuner.TryResolveStylusPressure(
            double.NaN,
            ClassroomWritingModeTuner.DefaultPseudoPressureLowThreshold,
            ClassroomWritingModeTuner.DefaultPseudoPressureHighThreshold,
            out _);
        var acceptedPositiveInfinity = ClassroomWritingModeTuner.TryResolveStylusPressure(
            double.PositiveInfinity,
            ClassroomWritingModeTuner.DefaultPseudoPressureLowThreshold,
            ClassroomWritingModeTuner.DefaultPseudoPressureHighThreshold,
            out _);
        var acceptedNegativeInfinity = ClassroomWritingModeTuner.TryResolveStylusPressure(
            double.NegativeInfinity,
            ClassroomWritingModeTuner.DefaultPseudoPressureLowThreshold,
            ClassroomWritingModeTuner.DefaultPseudoPressureHighThreshold,
            out _);

        acceptedNaN.Should().BeFalse();
        acceptedPositiveInfinity.Should().BeFalse();
        acceptedNegativeInfinity.Should().BeFalse();
    }
}
