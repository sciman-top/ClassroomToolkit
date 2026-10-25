using System.Windows;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        if ((SettingsTabControl?.SelectedIndex ?? 0) == 0)
        {
            var result = TopmostMessageBox.Show(
                this,
                "重置“笔触与预设”会同时恢复部分场景参数（如 WPS 策略、抬笔后刷新、缩放灵敏度）。是否继续？",
                "仅重置当前页",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        ApplyDefaultSettingsForCurrentTab();
    }

    private void OnRestoreAllDefaultsClick(object sender, RoutedEventArgs e)
    {
        var result = TopmostMessageBox.Show(
            this,
            "将恢复画笔设置窗口中的全部默认参数，是否继续？",
            "重置全部设置",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        ApplyDefaultSettings();
    }

    private void ApplyDefaultSettingsForCurrentTab()
    {
        var defaults = new AppSettings();
        var tabIndex = SettingsTabControl?.SelectedIndex ?? 0;
        var defaultPreset = ResolveInitialPresetScheme(defaults);

        _suppressPresetSelectionChanged = true;
        _suppressPresetAutoCustom = true;
        _suppressSectionDirtyTracking = true;
        try
        {
            switch (tabIndex)
            {
                case 0:
                    SelectComboByTag(PresetSchemeCombo, defaultPreset, PresetSchemeDefaults.Custom);
                    _currentPresetScheme = defaultPreset;
                    if (!IsCustomScheme(defaultPreset))
                    {
                        // Keep preset selection and managed parameters consistent.
                        ApplyPresetScheme(defaultPreset);
                    }
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
                    BrushColor = defaults.BrushColor;
                    BoardColor = defaults.BoardColor;
                    QuickColor1 = defaults.QuickColor1;
                    QuickColor2 = defaults.QuickColor2;
                    QuickColor3 = defaults.QuickColor3;
                    break;
                case 1:
                    SelectShapeType(defaults.ShapeType);
                    SelectComboByTag(ToolbarScaleCombo, FindNearestScale(defaults.PaintToolbarScale));
                    break;
                case 2:
                    SelectComboByTag(OfficeModeCombo, defaults.OfficeInputMode, WpsInputModeDefaults.Auto);
                    SelectComboByTag(WpsModeCombo, defaults.WpsInputMode, WpsInputModeDefaults.Auto);
                    ControlMsPptCheck.IsChecked = defaults.ControlMsPpt;
                    ControlWpsPptCheck.IsChecked = defaults.ControlWpsPpt;
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
                        NormalizePresentationClassifierOverridesJson(_workingPresentationClassifierOverridesJson);
                    PresentationClassifierOverridesJson = _workingPresentationClassifierOverridesJson;
                    ClearClassifierImportRollback();
                    RefreshPresentationClassifierPackageStatusText(
                        BuildClassifierPackageStatusFromOverrides(
                            _workingPresentationClassifierOverridesJson,
                             importedDetail: "已恢复参数默认值，保留现有学习覆盖。"));
                    ForceForegroundCheck.IsChecked = defaults.ForcePresentationForegroundOnFullscreen;
                    InkSaveCheck.IsChecked = defaults.InkSaveEnabled;
                    InkCacheCheck.IsChecked = defaults.InkCacheEnabled;
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
                    break;
                default:
                    ApplyDefaultSettings();
                    return;
            }
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
        UpdateClassroomWritingModeHint(ResolveClassroomWritingMode());
        if (tabIndex == 0 && IsCustomScheme(defaultPreset))
        {
            SaveCurrentAsCustomSnapshot();
        }

        UpdatePresetHint(GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom));
        ApplySceneCardsLayout(SceneCardsGrid?.ActualWidth ?? 0);
        UpdateSectionDirtyStates();
    }
}
