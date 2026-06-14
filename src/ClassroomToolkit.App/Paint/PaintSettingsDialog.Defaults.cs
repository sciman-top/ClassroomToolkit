using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void ApplyDefaultSettings()
    {
        var defaults = new AppSettings();
        var defaultPreset = ResolveInitialPresetScheme(defaults);

        _suppressPresetSelectionChanged = true;
        _suppressPresetAutoCustom = true;
        _suppressSectionDirtyTracking = true;
        try
        {
            SelectComboByTag(OfficeModeCombo, defaults.OfficeInputMode, WpsInputModeDefaults.Auto);
            SelectComboByTag(WpsModeCombo, defaults.WpsInputMode, WpsInputModeDefaults.Auto);
            SelectComboByTag(PresetSchemeCombo, defaultPreset, PresetSchemeDefaults.Custom);
            _currentPresetScheme = defaultPreset;
            SelectIntCombo(WpsDebounceCombo, defaults.WpsDebounceMs, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);
            SelectIntCombo(
                FallbackFailureThresholdCombo,
                defaults.PresentationAutoFallbackFailureThreshold,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault);
            SelectIntCombo(
                FallbackProbeIntervalCombo,
                defaults.PresentationAutoFallbackProbeIntervalCommands,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault);
            WpsWheelCheck.IsChecked = defaults.WpsWheelForward;
            LockStrategyOnDegradeCheck.IsChecked = defaults.PresentationLockStrategyWhenDegraded;
            PresentationClassifierAutoLearnCheck.IsChecked = defaults.PresentationClassifierAutoLearnEnabled;
            PresentationClassifierClearOverridesCheck.IsChecked = false;
            _workingPresentationClassifierOverridesJson =
                NormalizePresentationClassifierOverridesJson(defaults.PresentationClassifierOverridesJson);
            PresentationClassifierOverridesJson = _workingPresentationClassifierOverridesJson;
            ClearClassifierImportRollback();
            RefreshPresentationClassifierPackageStatusText(
                BuildClassifierPackageStatusFromOverrides(
                    _workingPresentationClassifierOverridesJson,
                    importedDetail: "已恢复默认覆盖规则。"));
            ForceForegroundCheck.IsChecked = defaults.ForcePresentationForegroundOnFullscreen;

            InkSaveCheck.IsChecked = defaults.InkSaveEnabled;
            SelectInkExportScope(defaults.InkExportScope);
            SelectIntCombo(ExportParallelCombo, defaults.InkExportMaxParallelFiles, fallback: PaintSettingsOptionDefaults.InkExportMaxParallelDefault);
            SelectIntCombo(NeighborPrefetchCombo, defaults.PhotoNeighborPrefetchRadiusMax, fallback: PaintSettingsOptionDefaults.PhotoNeighborPrefetchRadiusDefault);
            SelectIntCombo(PostInputRefreshDelayCombo, defaults.PhotoPostInputRefreshDelayMs, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs);
            SelectDoubleCombo(WheelZoomBaseCombo, defaults.PhotoWheelZoomBase, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault);
            SelectDoubleCombo(GestureSensitivityCombo, defaults.PhotoGestureZoomSensitivity, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault);
            SelectComboByTag(PhotoInertiaProfileCombo, defaults.PhotoInertiaProfile, PhotoInertiaProfileDefaults.Standard);
            PhotoInputTelemetryCheck.IsChecked = defaults.PhotoInputTelemetryEnabled;
            PhotoRememberTransformCheck.IsChecked = defaults.PhotoRememberTransform;
            PhotoCrossPageDisplayCheck.IsChecked = defaults.PhotoCrossPageDisplay;

            SelectBrushStyle(defaults.BrushStyle);
            SelectWhiteboardPreset(defaults.WhiteboardPreset);
            SelectCalligraphyPreset(defaults.CalligraphyPreset);
            SelectClassroomWritingMode(defaults.ClassroomWritingMode);
            CalligraphyInkBloomCheck.IsChecked = defaults.CalligraphyInkBloomEnabled;
            CalligraphySealCheck.IsChecked = defaults.CalligraphySealEnabled;
            BrushSizeSlider.Value = Clamp(defaults.QuickBrushSize1, 1, 50);
            BrushSize2Slider.Value = Clamp(defaults.QuickBrushSize2, 1, 50);
            BrushSize3Slider.Value = Clamp(defaults.QuickBrushSize3, 1, 50);
            EraserSizeSlider.Value = Clamp(defaults.EraserSize, 6, 60);
            BrushOpacitySlider.Value = ToPercent(defaults.BrushOpacity);
            CalligraphyOverlayThresholdSlider.Value = ToPercent(defaults.CalligraphyOverlayOpacityThreshold);
            SelectShapeType(defaults.ShapeType);
            SelectComboByTag(ToolbarScaleCombo, FindNearestScale(defaults.PaintToolbarScale));
            QuickColor1 = defaults.QuickColor1;
            QuickColor2 = defaults.QuickColor2;
            QuickColor3 = defaults.QuickColor3;
            QuickBrushSize1 = defaults.QuickBrushSize1;
            QuickBrushSize2 = defaults.QuickBrushSize2;
            QuickBrushSize3 = defaults.QuickBrushSize3;
        }
        finally
        {
            _suppressPresetSelectionChanged = false;
            _suppressPresetAutoCustom = false;
            _suppressSectionDirtyTracking = false;
        }

        UpdateCalligraphyOptionState();
        UpdateBrushSizeLabel();
        UpdateBrushOpacityLabel();
        UpdateEraserSizeLabel();
        UpdateCalligraphyOverlayThresholdLabel();
        UpdateClassroomWritingModeHint(defaults.ClassroomWritingMode);
        if (IsCustomScheme(defaultPreset))
        {
            SaveCurrentAsCustomSnapshot();
        }

        UpdatePresetHint(defaultPreset);
        ApplySceneCardsLayout(SceneCardsGrid?.ActualWidth ?? 0);
        UpdateSectionDirtyStates();
    }
}
