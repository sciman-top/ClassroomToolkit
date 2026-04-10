using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;
using MediaColor = System.Windows.Media.Color;
using MediaColors = System.Windows.Media.Colors;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog : Window
{
    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
        if (_sizeToContentCommitted)
        {
            return;
        }
        if (!DispatcherInvokeAvailabilityPolicy.CanBeginInvoke(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            return;
        }

        var scheduled = PaintActionInvoker.TryInvoke(() =>
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    _sizeToContentCommitted = true;
                    SizeToContent = System.Windows.SizeToContent.Manual;
                },
                System.Windows.Threading.DispatcherPriority.ContextIdle);
            return true;
        }, fallback: false);
        if (!scheduled)
        {
            if (Dispatcher.CheckAccess())
            {
                _sizeToContentCommitted = true;
                SizeToContent = System.Windows.SizeToContent.Manual;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PaintSettingsDialog] deferred SizeToContent scheduling skipped");
            }
        }
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        DetachSectionDirtyTrackingHandlers();
        DetachPresetManagedControlHandlers();
        ClassroomWritingModeCombo.SelectionChanged -= OnClassroomWritingModeChanged;
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        // The current dialog no longer exposes PPT/WPS control toggles;
        // keep the existing persisted values instead of forcing them on.
        OfficeInputMode = GetSelectedTag(OfficeModeCombo, WpsInputModeDefaults.Auto);
        WpsInputMode = GetSelectedTag(WpsModeCombo, WpsInputModeDefaults.Auto);
        PresetScheme = GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom);
        WpsWheelForward = WpsWheelCheck.IsChecked == true;
        WpsDebounceMs = ResolveIntCombo(WpsDebounceCombo, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);
        PresentationLockStrategyWhenDegraded = LockStrategyOnDegradeCheck.IsChecked != false;
        PresentationAutoFallbackFailureThreshold = ResolveIntCombo(
            FallbackFailureThresholdCombo,
            fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault);
        PresentationAutoFallbackProbeIntervalCommands = ResolveIntCombo(
            FallbackProbeIntervalCombo,
            fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault);
        PresentationClassifierAutoLearnEnabled = PresentationClassifierAutoLearnCheck.IsChecked == true;
        PresentationClassifierClearOverridesRequested = PresentationClassifierClearOverridesCheck.IsChecked == true;
        PresentationClassifierOverridesJson = PresentationClassifierClearOverridesRequested
            ? string.Empty
            : _workingPresentationClassifierOverridesJson;
        ForcePresentationForegroundOnFullscreen = ForceForegroundCheck.IsChecked == true;
        BrushSize = Clamp(BrushSizeSlider.Value, 1, 50);
        EraserSize = Clamp(EraserSizeSlider.Value, 6, 60);
        BrushOpacity = ToByte(BrushOpacitySlider.Value);
        BrushStyle = ResolveBrushStyle();
        WhiteboardPreset = ResolveWhiteboardPreset();
        CalligraphyPreset = ResolveCalligraphyPreset();
        ClassroomWritingMode = ResolveClassroomWritingMode();
        CalligraphyInkBloomEnabled = CalligraphyInkBloomCheck.IsChecked == true;
        CalligraphySealEnabled = CalligraphySealCheck.IsChecked == true;
        CalligraphyOverlayOpacityThreshold = ToByte(CalligraphyOverlayThresholdSlider.Value);
        ShapeType = ResolveShapeType();
        ToolbarScale = GetSelectedScale();
        InkSaveEnabled = InkSaveCheck.IsChecked == true;
        InkExportScope = ResolveInkExportScope();
        InkExportMaxParallelFiles = ResolveIntCombo(
            ExportParallelCombo,
            fallback: PaintSettingsOptionDefaults.InkExportMaxParallelDefault);
        PhotoRememberTransform = PhotoRememberTransformCheck.IsChecked == true;
        PhotoCrossPageDisplay = PhotoCrossPageDisplayCheck.IsChecked == true;
        PhotoInputTelemetryEnabled = PhotoInputTelemetryCheck.IsChecked == true;
        PhotoNeighborPrefetchRadiusMax = ResolveIntCombo(
            NeighborPrefetchCombo,
            fallback: PaintSettingsOptionDefaults.PhotoNeighborPrefetchRadiusDefault);
        PhotoPostInputRefreshDelayMs = ResolveIntCombo(PostInputRefreshDelayCombo, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs);
        PhotoWheelZoomBase = ResolveDoubleCombo(WheelZoomBaseCombo, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault);
        PhotoGestureZoomSensitivity = ResolveDoubleCombo(GestureSensitivityCombo, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault);
        PhotoInertiaProfile = PhotoInertiaProfileDefaults.Normalize(
            GetSelectedTag(PhotoInertiaProfileCombo, PhotoInertiaProfileDefaults.Standard));
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        if ((SettingsTabControl?.SelectedIndex ?? 0) == 0)
        {
            var result = System.Windows.MessageBox.Show(
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
        var result = System.Windows.MessageBox.Show(
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
                    BrushSizeSlider.Value = Clamp(defaults.BrushSize, 1, 50);
                    EraserSizeSlider.Value = Clamp(defaults.EraserSize, 6, 60);
                    BrushOpacitySlider.Value = ToPercent(defaults.BrushOpacity);
                    CalligraphyOverlayThresholdSlider.Value = ToPercent(defaults.CalligraphyOverlayOpacityThreshold);
                    break;
                case 1:
                    SelectShapeType(defaults.ShapeType);
                    SelectComboByTag(ToolbarScaleCombo, FindNearestScale(defaults.PaintToolbarScale));
                    break;
                case 2:
                    SelectComboByTag(OfficeModeCombo, defaults.OfficeInputMode, WpsInputModeDefaults.Auto);
                    SelectComboByTag(WpsModeCombo, defaults.WpsInputMode, WpsInputModeDefaults.Auto);
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

    private void OnBrushSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateBrushSizeLabel();
    }

    private void OnBrushOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateBrushOpacityLabel();
    }

    private void OnCalligraphyOverlayThresholdChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateCalligraphyOverlayThresholdLabel();
    }

    private void OnEraserSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateEraserSizeLabel();
    }

    private void OnBrushStyleChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateCalligraphyOptionState();
    }


}
