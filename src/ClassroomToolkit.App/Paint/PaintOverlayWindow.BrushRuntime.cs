using System;
using ClassroomToolkit.App.Paint.Brushes;
using MediaColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    public void SetBrushOpacity(byte opacity)
    {
        _brushOpacity = opacity;
    }

    public void SetCalligraphyOptions(bool inkBloomEnabled, bool sealEnabled)
    {
        _calligraphyInkBloomEnabled = inkBloomEnabled;
        _calligraphySealEnabled = sealEnabled;
    }

    public void SetCalligraphyOverlayOpacityThreshold(byte threshold)
    {
        _calligraphyOverlayOpacityThreshold = threshold;
    }

    public void SetClassroomWritingMode(ClassroomWritingMode mode)
    {
        _classroomWritingMode = mode;
        _stylusPressureAnalyzer.Reset();
        _stylusPressureCalibrator.Reset();
        _stylusDeviceAdaptiveProfiler.Reset();
        ApplyClassroomRuntimeProfile();
        EnsureActiveRenderer(force: true);
    }

    public void RestoreStylusAdaptiveState(
        int pressureProfile,
        int sampleRateTier,
        int predictionHorizonMs,
        double calibratedLow,
        double calibratedHigh)
    {
        StylusPressureDeviceProfile resolvedPressure = StylusPressureDeviceProfile.Unknown;
        StylusSampleRateTier resolvedRate = StylusSampleRateTier.Unknown;

        if (Enum.IsDefined(typeof(StylusPressureDeviceProfile), pressureProfile))
        {
            resolvedPressure = (StylusPressureDeviceProfile)pressureProfile;
        }
        if (Enum.IsDefined(typeof(StylusSampleRateTier), sampleRateTier))
        {
            resolvedRate = (StylusSampleRateTier)sampleRateTier;
        }

        _stylusDeviceAdaptiveProfiler.Seed(resolvedPressure, resolvedRate, predictionHorizonMs);
        if (calibratedHigh - calibratedLow >= StylusRuntimeDefaults.CalibratedRangeSeedMinWidth)
        {
            _stylusPressureCalibrator.SeedRange(calibratedLow, calibratedHigh);
        }
        ApplyClassroomRuntimeProfile();
        EnsureActiveRenderer(force: true);
    }

    public bool TryGetStylusAdaptiveState(
        out int pressureProfile,
        out int sampleRateTier,
        out int predictionHorizonMs,
        out double calibratedLow,
        out double calibratedHigh)
    {
        var profile = _stylusDeviceAdaptiveProfiler.CurrentProfile;
        pressureProfile = (int)profile.PressureProfile;
        sampleRateTier = (int)profile.SampleRateTier;
        predictionHorizonMs = profile.PredictionHorizonMs;

        if (_stylusPressureCalibrator.TryExportRange(out calibratedLow, out calibratedHigh))
        {
            return true;
        }

        calibratedLow = StylusRuntimeDefaults.CalibratedLowDefault;
        calibratedHigh = StylusRuntimeDefaults.CalibratedHighDefault;
        return pressureProfile != 0 || sampleRateTier != 0;
    }

    public void SetBrushTuning(WhiteboardBrushPreset whiteboardPreset, CalligraphyBrushPreset calligraphyPreset)
    {
        _whiteboardPreset = whiteboardPreset;
        _calligraphyPreset = calligraphyPreset;
        EnsureActiveRenderer(force: true);
    }

    public void SetBrushStyle(PaintBrushStyle style)
    {
        _brushStyle = style;
        EnsureActiveRenderer(force: true);

        // Refresh mode to apply correct input handling
        SetMode(_mode);
    }

    private void ApplyClassroomRuntimeProfile()
    {
        var runtime = ClassroomWritingModeTuner.ResolveRuntimeSettings(_classroomWritingMode);
        _stylusPseudoPressureLowThreshold = runtime.PseudoPressureLowThreshold;
        _stylusPseudoPressureHighThreshold = runtime.PseudoPressureHighThreshold;
        _calligraphyPreviewMinDistance = runtime.CalligraphyPreviewMinDistance;
        _brushPredictionHorizonMs = _stylusDeviceAdaptiveProfiler.CurrentProfile.PredictionHorizonMs;
    }

    private MarkerBrushConfig BuildMarkerConfig()
    {
        var config = _whiteboardPreset switch
        {
            WhiteboardBrushPreset.Sharp => MarkerBrushConfig.Sharp,
            WhiteboardBrushPreset.Balanced => MarkerBrushConfig.Balanced,
            _ => MarkerBrushConfig.Smooth
        };

        ClassroomWritingModeTuner.ApplyToMarkerConfig(config, _classroomWritingMode);
        StylusDeviceAdaptiveProfiler.ApplyToMarkerConfig(config, _stylusDeviceAdaptiveProfiler.CurrentProfile);
        return config;
    }

    private BrushPhysicsConfig BuildCalligraphyConfig()
    {
        _calligraphyRenderMode = ResolveCalligraphyRenderMode(_calligraphyPreset);
        var config = _calligraphyPreset switch
        {
            CalligraphyBrushPreset.Sharp => BrushPhysicsConfig.CreateCalligraphySharp(),
            CalligraphyBrushPreset.Soft => BrushPhysicsConfig.CreateCalligraphyInkFeel(),
            _ => BrushPhysicsConfig.CreateCalligraphyClarity()
        };

        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(config, _classroomWritingMode);
        StylusDeviceAdaptiveProfiler.ApplyToCalligraphyConfig(config, _stylusDeviceAdaptiveProfiler.CurrentProfile);
        return config;
    }

    private static CalligraphyRenderMode ResolveCalligraphyRenderMode(CalligraphyBrushPreset preset)
    {
        return preset == CalligraphyBrushPreset.Soft
            ? CalligraphyRenderMode.Ink
            : CalligraphyRenderMode.Clarity;
    }

    private void EnsureActiveRenderer(bool force = false)
    {
        ApplyClassroomRuntimeProfile();
        if (!force && _activeRenderer != null && _inkRendererFactory.CanReuse(_brushStyle, _activeRenderer))
        {
            return;
        }

        var markerConfig = BuildMarkerConfig();
        var calligraphyConfig = BuildCalligraphyConfig();
        _activeRenderer = _inkRendererFactory.Create(_brushStyle, markerConfig, calligraphyConfig);
    }

    private MediaColor EffectiveBrushColor()
    {
        return MediaColor.FromArgb(_brushOpacity, _brushColor.R, _brushColor.G, _brushColor.B);
    }
}
