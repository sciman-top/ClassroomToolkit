using System;
using System.Windows;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void AttachSectionDirtyTrackingHandlers()
    {
        BrushStyleCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        WhiteboardPresetCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        CalligraphyPresetCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        PresetSchemeCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        ClassroomWritingModeCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        InkExportScopeCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        ExportParallelCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        NeighborPrefetchCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        PostInputRefreshDelayCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        WheelZoomBaseCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        GestureSensitivityCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        PhotoInertiaProfileCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        OfficeModeCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        WpsModeCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        WpsDebounceCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        FallbackFailureThresholdCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        FallbackProbeIntervalCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        ShapeCombo.SelectionChanged += OnSectionDirtySelectionChanged;
        ToolbarScaleCombo.SelectionChanged += OnSectionDirtySelectionChanged;

        BrushSizeSlider.ValueChanged += OnSectionDirtyValueChanged;
        BrushOpacitySlider.ValueChanged += OnSectionDirtyValueChanged;
        EraserSizeSlider.ValueChanged += OnSectionDirtyValueChanged;
        CalligraphyOverlayThresholdSlider.ValueChanged += OnSectionDirtyValueChanged;

        CalligraphyInkBloomCheck.Checked += OnSectionDirtyRoutedChanged;
        CalligraphyInkBloomCheck.Unchecked += OnSectionDirtyRoutedChanged;
        CalligraphySealCheck.Checked += OnSectionDirtyRoutedChanged;
        CalligraphySealCheck.Unchecked += OnSectionDirtyRoutedChanged;
        InkSaveCheck.Checked += OnSectionDirtyRoutedChanged;
        InkSaveCheck.Unchecked += OnSectionDirtyRoutedChanged;
        PhotoCrossPageDisplayCheck.Checked += OnSectionDirtyRoutedChanged;
        PhotoCrossPageDisplayCheck.Unchecked += OnSectionDirtyRoutedChanged;
        PhotoRememberTransformCheck.Checked += OnSectionDirtyRoutedChanged;
        PhotoRememberTransformCheck.Unchecked += OnSectionDirtyRoutedChanged;
        PhotoInputTelemetryCheck.Checked += OnSectionDirtyRoutedChanged;
        PhotoInputTelemetryCheck.Unchecked += OnSectionDirtyRoutedChanged;
        WpsWheelCheck.Checked += OnSectionDirtyRoutedChanged;
        WpsWheelCheck.Unchecked += OnSectionDirtyRoutedChanged;
        ForceForegroundCheck.Checked += OnSectionDirtyRoutedChanged;
        ForceForegroundCheck.Unchecked += OnSectionDirtyRoutedChanged;
        LockStrategyOnDegradeCheck.Checked += OnSectionDirtyRoutedChanged;
        LockStrategyOnDegradeCheck.Unchecked += OnSectionDirtyRoutedChanged;
        PresentationClassifierAutoLearnCheck.Checked += OnSectionDirtyRoutedChanged;
        PresentationClassifierAutoLearnCheck.Unchecked += OnSectionDirtyRoutedChanged;
        PresentationClassifierClearOverridesCheck.Checked += OnSectionDirtyRoutedChanged;
        PresentationClassifierClearOverridesCheck.Unchecked += OnSectionDirtyRoutedChanged;
    }

    private void DetachSectionDirtyTrackingHandlers()
    {
        BrushStyleCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        WhiteboardPresetCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        CalligraphyPresetCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        PresetSchemeCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        ClassroomWritingModeCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        InkExportScopeCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        ExportParallelCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        NeighborPrefetchCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        PostInputRefreshDelayCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        WheelZoomBaseCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        GestureSensitivityCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        PhotoInertiaProfileCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        OfficeModeCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        WpsModeCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        WpsDebounceCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        FallbackFailureThresholdCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        FallbackProbeIntervalCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        ShapeCombo.SelectionChanged -= OnSectionDirtySelectionChanged;
        ToolbarScaleCombo.SelectionChanged -= OnSectionDirtySelectionChanged;

        BrushSizeSlider.ValueChanged -= OnSectionDirtyValueChanged;
        BrushOpacitySlider.ValueChanged -= OnSectionDirtyValueChanged;
        EraserSizeSlider.ValueChanged -= OnSectionDirtyValueChanged;
        CalligraphyOverlayThresholdSlider.ValueChanged -= OnSectionDirtyValueChanged;

        CalligraphyInkBloomCheck.Checked -= OnSectionDirtyRoutedChanged;
        CalligraphyInkBloomCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        CalligraphySealCheck.Checked -= OnSectionDirtyRoutedChanged;
        CalligraphySealCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        InkSaveCheck.Checked -= OnSectionDirtyRoutedChanged;
        InkSaveCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        PhotoCrossPageDisplayCheck.Checked -= OnSectionDirtyRoutedChanged;
        PhotoCrossPageDisplayCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        PhotoRememberTransformCheck.Checked -= OnSectionDirtyRoutedChanged;
        PhotoRememberTransformCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        PhotoInputTelemetryCheck.Checked -= OnSectionDirtyRoutedChanged;
        PhotoInputTelemetryCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        WpsWheelCheck.Checked -= OnSectionDirtyRoutedChanged;
        WpsWheelCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        ForceForegroundCheck.Checked -= OnSectionDirtyRoutedChanged;
        ForceForegroundCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        LockStrategyOnDegradeCheck.Checked -= OnSectionDirtyRoutedChanged;
        LockStrategyOnDegradeCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        PresentationClassifierAutoLearnCheck.Checked -= OnSectionDirtyRoutedChanged;
        PresentationClassifierAutoLearnCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        PresentationClassifierClearOverridesCheck.Checked -= OnSectionDirtyRoutedChanged;
        PresentationClassifierClearOverridesCheck.Unchecked -= OnSectionDirtyRoutedChanged;
    }

    private void OnSectionDirtySelectionChanged(object? sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateSectionDirtyStates();
    }

    private void OnSectionDirtyValueChanged(object? sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSectionDirtyStates();
    }

    private void OnSectionDirtyRoutedChanged(object? sender, RoutedEventArgs e)
    {
        UpdateSectionDirtyStates();
    }

    private PresetBrushSectionState CapturePresetBrushSectionStateFromControls()
    {
        return new PresetBrushSectionState(
            PresetScheme: GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom),
            BrushStyle: ResolveBrushStyle(),
            WhiteboardPreset: ResolveWhiteboardPreset(),
            CalligraphyPreset: ResolveCalligraphyPreset(),
            ClassroomWritingMode: ResolveClassroomWritingMode(),
            BrushSizePx: (int)Math.Round(BrushSizeSlider.Value),
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
            SelectBrushStyle(state.BrushStyle);
            SelectWhiteboardPreset(state.WhiteboardPreset);
            SelectCalligraphyPreset(state.CalligraphyPreset);
            SelectClassroomWritingMode(state.ClassroomWritingMode);
            BrushSizeSlider.Value = Clamp(state.BrushSizePx, 1, 50);
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
        UpdateClassroomWritingModeHint(state.ClassroomWritingMode);
        UpdatePresetHint(state.PresetScheme);
    }

    private void ApplySceneSectionState(SceneSectionState state)
    {
        _suppressSectionDirtyTracking = true;
        _suppressPresetAutoCustom = true;
        try
        {
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
            BrushSizeSlider.Value = Clamp(defaults.BrushSize, 1, 50);
            EraserSizeSlider.Value = Clamp(defaults.EraserSize, 6, 60);
            BrushOpacitySlider.Value = ToPercent(defaults.BrushOpacity);
            CalligraphyOverlayThresholdSlider.Value = ToPercent(defaults.CalligraphyOverlayOpacityThreshold);
            SelectShapeType(defaults.ShapeType);
            SelectComboByTag(ToolbarScaleCombo, FindNearestScale(defaults.PaintToolbarScale));
            QuickColor1 = defaults.QuickColor1;
            QuickColor2 = defaults.QuickColor2;
            QuickColor3 = defaults.QuickColor3;
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
