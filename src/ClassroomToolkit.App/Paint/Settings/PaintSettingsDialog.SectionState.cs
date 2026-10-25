using System;
using System.Windows;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private PresetBrushSectionState CapturePresetBrushSectionStateFromControls()
    {
        return new PresetBrushSectionState(
            PresetScheme: GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom),
            BrushColor: BrushColor,
            BoardColor: BoardColor,
            QuickColor1: QuickColor1,
            QuickColor2: QuickColor2,
            QuickColor3: QuickColor3,
            BrushStyle: ResolveBrushStyle(),
            WhiteboardPreset: ResolveWhiteboardPreset(),
            CalligraphyPreset: ResolveCalligraphyPreset(),
            ClassroomWritingMode: ResolveClassroomWritingMode(),
            QuickBrushSize1Px: (int)Math.Round(BrushSizeSlider.Value),
            QuickBrushSize2Px: (int)Math.Round(BrushSize2Slider.Value),
            QuickBrushSize3Px: (int)Math.Round(BrushSize3Slider.Value),
            BrushOpacityPercent: (int)Math.Round(BrushOpacitySlider.Value),
            EraserSizePx: (int)Math.Round(EraserSizeSlider.Value),
            CalligraphyInkBloomEnabled: CalligraphyInkBloomCheck.IsChecked == true,
            CalligraphySealEnabled: CalligraphySealCheck.IsChecked == true,
            CalligraphyOverlayThresholdPercent: (int)Math.Round(CalligraphyOverlayThresholdSlider.Value),
            WpsInputMode: GetSelectedTag(WpsModeCombo, WpsInputModeDefaults.Auto),
            WpsWheelForward: WpsWheelCheck.IsChecked == true,
            LockStrategyWhenDegraded: LockStrategyOnDegradeCheck.IsChecked != false,
            AutoFallbackFailureThreshold: ResolveIntCombo(
                FallbackFailureThresholdCombo,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault),
            AutoFallbackProbeIntervalCommands: ResolveIntCombo(
                FallbackProbeIntervalCombo,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault),
            WpsDebounceMs: ResolveIntCombo(WpsDebounceCombo, fallback: PaintPresetDefaults.WpsDebounceDefaultMs),
            PhotoPostInputRefreshDelayMs: ResolveIntCombo(PostInputRefreshDelayCombo, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs),
            PhotoWheelZoomBase: ResolveDoubleCombo(WheelZoomBaseCombo, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault),
            PhotoGestureZoomSensitivity: ResolveDoubleCombo(GestureSensitivityCombo, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault),
            PhotoInertiaProfile: PhotoInertiaProfileDefaults.Normalize(GetSelectedTag(PhotoInertiaProfileCombo, PhotoInertiaProfileDefaults.Standard)));
    }

    private SceneSectionState CaptureSceneSectionStateFromControls()
    {
        return new SceneSectionState(
            ControlMsPpt: ControlMsPptCheck.IsChecked == true,
            ControlWpsPpt: ControlWpsPptCheck.IsChecked == true,
            InkCacheEnabled: InkCacheCheck.IsChecked == true,
            InkSaveEnabled: InkSaveCheck.IsChecked == true,
            InkExportScope: ResolveInkExportScope(),
            InkExportMaxParallelFiles: ResolveIntCombo(ExportParallelCombo, fallback: PaintSettingsOptionDefaults.InkExportMaxParallelDefault),
            PhotoCrossPageDisplay: PhotoCrossPageDisplayCheck.IsChecked == true,
            PhotoRememberTransform: PhotoRememberTransformCheck.IsChecked == true,
            PhotoInputTelemetryEnabled: PhotoInputTelemetryCheck.IsChecked == true,
            PhotoNeighborPrefetchRadiusMax: ResolveIntCombo(NeighborPrefetchCombo, fallback: PaintSettingsOptionDefaults.PhotoNeighborPrefetchRadiusDefault),
            PhotoPostInputRefreshDelayMs: ResolveIntCombo(PostInputRefreshDelayCombo, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs),
            PhotoWheelZoomBase: ResolveDoubleCombo(WheelZoomBaseCombo, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault),
            PhotoGestureZoomSensitivity: ResolveDoubleCombo(GestureSensitivityCombo, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault),
            PhotoInertiaProfile: PhotoInertiaProfileDefaults.Normalize(GetSelectedTag(PhotoInertiaProfileCombo, PhotoInertiaProfileDefaults.Standard)),
            OfficeInputMode: GetSelectedTag(OfficeModeCombo, WpsInputModeDefaults.Auto),
            WpsInputMode: GetSelectedTag(WpsModeCombo, WpsInputModeDefaults.Auto),
            WpsWheelForward: WpsWheelCheck.IsChecked == true,
            ForcePresentationForegroundOnFullscreen: ForceForegroundCheck.IsChecked == true,
            WpsDebounceMs: ResolveIntCombo(WpsDebounceCombo, fallback: PaintPresetDefaults.WpsDebounceDefaultMs),
            LockStrategyWhenDegraded: LockStrategyOnDegradeCheck.IsChecked != false,
            PresentationAutoFallbackFailureThreshold: ResolveIntCombo(
                FallbackFailureThresholdCombo,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault),
            PresentationAutoFallbackProbeIntervalCommands: ResolveIntCombo(
                FallbackProbeIntervalCombo,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault),
            PresentationClassifierAutoLearnEnabled: PresentationClassifierAutoLearnCheck.IsChecked == true,
            PresentationClassifierClearOverridesRequested: PresentationClassifierClearOverridesCheck.IsChecked == true,
            PresentationClassifierOverridesJson: _workingPresentationClassifierOverridesJson);
    }

    private AdvancedSectionState CaptureAdvancedSectionStateFromControls()
    {
        return new AdvancedSectionState(
            ShapeType: ResolveShapeType(),
            ToolbarScale: GetSelectedScale());
    }

    private void ApplyPresetBrushSectionState(PresetBrushSectionState state)
    {
        _suppressSectionDirtyTracking = true;
        _suppressPresetSelectionChanged = true;
        _suppressPresetAutoCustom = true;
        try
        {
            SelectComboByTag(PresetSchemeCombo, state.PresetScheme, PresetSchemeDefaults.Custom);
            BrushColor = state.BrushColor;
            BoardColor = state.BoardColor;
            QuickColor1 = state.QuickColor1;
            QuickColor2 = state.QuickColor2;
            QuickColor3 = state.QuickColor3;
            SelectBrushStyle(state.BrushStyle);
            SelectWhiteboardPreset(state.WhiteboardPreset);
            SelectCalligraphyPreset(state.CalligraphyPreset);
            SelectClassroomWritingMode(state.ClassroomWritingMode);
            BrushSizeSlider.Value = Clamp(state.QuickBrushSize1Px, 1, 50);
            BrushSize2Slider.Value = Clamp(state.QuickBrushSize2Px, 1, 50);
            BrushSize3Slider.Value = Clamp(state.QuickBrushSize3Px, 1, 50);
            BrushOpacitySlider.Value = Clamp(state.BrushOpacityPercent, 0, 100);
            EraserSizeSlider.Value = Clamp(state.EraserSizePx, 6, 60);
            CalligraphyInkBloomCheck.IsChecked = state.CalligraphyInkBloomEnabled;
            CalligraphySealCheck.IsChecked = state.CalligraphySealEnabled;
            CalligraphyOverlayThresholdSlider.Value = Clamp(state.CalligraphyOverlayThresholdPercent, 0, 100);
            SelectComboByTag(WpsModeCombo, state.WpsInputMode, WpsInputModeDefaults.Auto);
            WpsWheelCheck.IsChecked = state.WpsWheelForward;
            LockStrategyOnDegradeCheck.IsChecked = state.LockStrategyWhenDegraded;
            SelectIntCombo(
                FallbackFailureThresholdCombo,
                state.AutoFallbackFailureThreshold,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault);
            SelectIntCombo(
                FallbackProbeIntervalCombo,
                state.AutoFallbackProbeIntervalCommands,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault);
            SelectIntCombo(WpsDebounceCombo, state.WpsDebounceMs, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);
            SelectIntCombo(PostInputRefreshDelayCombo, state.PhotoPostInputRefreshDelayMs, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs);
            SelectDoubleCombo(WheelZoomBaseCombo, state.PhotoWheelZoomBase, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault);
            SelectDoubleCombo(GestureSensitivityCombo, state.PhotoGestureZoomSensitivity, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault);
            SelectComboByTag(PhotoInertiaProfileCombo, state.PhotoInertiaProfile, PhotoInertiaProfileDefaults.Standard);
        }
        finally
        {
            _suppressPresetAutoCustom = false;
            _suppressPresetSelectionChanged = false;
            _suppressSectionDirtyTracking = false;
        }

        _currentPresetScheme = state.PresetScheme;
        if (IsCustomScheme(state.PresetScheme))
        {
            SaveCurrentAsCustomSnapshot();
        }
        UpdateCalligraphyOptionState();
        UpdateBrushSizeLabel();
        UpdateBrushOpacityLabel();
        UpdateEraserSizeLabel();
        UpdateClassroomWritingModeHint(state.ClassroomWritingMode);
        UpdatePresetHint(state.PresetScheme);
    }

    private void ApplySceneSectionState(SceneSectionState state)
    {
        _suppressSectionDirtyTracking = true;
        _suppressPresetAutoCustom = true;
        try
        {
            ControlMsPptCheck.IsChecked = state.ControlMsPpt;
            ControlWpsPptCheck.IsChecked = state.ControlWpsPpt;
            InkCacheCheck.IsChecked = state.InkCacheEnabled;
            InkSaveCheck.IsChecked = state.InkSaveEnabled;
            SelectInkExportScope(state.InkExportScope);
            SelectIntCombo(ExportParallelCombo, state.InkExportMaxParallelFiles, fallback: PaintSettingsOptionDefaults.InkExportMaxParallelDefault);
            PhotoCrossPageDisplayCheck.IsChecked = state.PhotoCrossPageDisplay;
            PhotoRememberTransformCheck.IsChecked = state.PhotoRememberTransform;
            PhotoInputTelemetryCheck.IsChecked = state.PhotoInputTelemetryEnabled;
            SelectIntCombo(NeighborPrefetchCombo, state.PhotoNeighborPrefetchRadiusMax, fallback: PaintSettingsOptionDefaults.PhotoNeighborPrefetchRadiusDefault);
            SelectIntCombo(PostInputRefreshDelayCombo, state.PhotoPostInputRefreshDelayMs, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs);
            SelectDoubleCombo(WheelZoomBaseCombo, state.PhotoWheelZoomBase, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault);
            SelectDoubleCombo(GestureSensitivityCombo, state.PhotoGestureZoomSensitivity, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault);
            SelectComboByTag(PhotoInertiaProfileCombo, state.PhotoInertiaProfile, PhotoInertiaProfileDefaults.Standard);
            SelectComboByTag(OfficeModeCombo, state.OfficeInputMode, WpsInputModeDefaults.Auto);
            SelectComboByTag(WpsModeCombo, state.WpsInputMode, WpsInputModeDefaults.Auto);
            WpsWheelCheck.IsChecked = state.WpsWheelForward;
            ForceForegroundCheck.IsChecked = state.ForcePresentationForegroundOnFullscreen;
            SelectIntCombo(WpsDebounceCombo, state.WpsDebounceMs, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);
            LockStrategyOnDegradeCheck.IsChecked = state.LockStrategyWhenDegraded;
            SelectIntCombo(
                FallbackFailureThresholdCombo,
                state.PresentationAutoFallbackFailureThreshold,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault);
            SelectIntCombo(
                FallbackProbeIntervalCombo,
                state.PresentationAutoFallbackProbeIntervalCommands,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault);
            PresentationClassifierAutoLearnCheck.IsChecked = state.PresentationClassifierAutoLearnEnabled;
            PresentationClassifierClearOverridesCheck.IsChecked = state.PresentationClassifierClearOverridesRequested;
            _workingPresentationClassifierOverridesJson =
                NormalizePresentationClassifierOverridesJson(state.PresentationClassifierOverridesJson);
            PresentationClassifierOverridesJson = _workingPresentationClassifierOverridesJson;
        }
        finally
        {
            _suppressPresetAutoCustom = false;
            _suppressSectionDirtyTracking = false;
        }

        RefreshPresentationClassifierPackageStatusText(
            BuildClassifierPackageStatusFromOverrides(
                _workingPresentationClassifierOverridesJson,
                importedDetail: null));
        UpdatePresetHint(GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom));
    }

    private void ApplyAdvancedSectionState(AdvancedSectionState state)
    {
        _suppressSectionDirtyTracking = true;
        try
        {
            SelectShapeType(state.ShapeType);
            SelectComboByTag(ToolbarScaleCombo, state.ToolbarScale);
        }
        finally
        {
            _suppressSectionDirtyTracking = false;
        }
    }
}
