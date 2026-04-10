using System.Globalization;
using ClassroomToolkit.App.Settings;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void InitializeFromSettings(AppSettings settings)
    {
        ControlMsPpt = settings.ControlMsPpt;
        ControlWpsPpt = settings.ControlWpsPpt;
        BrushColor = settings.BrushColor;
        QuickColor1 = settings.QuickColor1;
        QuickColor2 = settings.QuickColor2;
        QuickColor3 = settings.QuickColor3;
        _workingPresentationClassifierOverridesJson =
            NormalizePresentationClassifierOverridesJson(settings.PresentationClassifierOverridesJson);
        PresentationClassifierOverridesJson = _workingPresentationClassifierOverridesJson;
        ClearClassifierImportRollback();

        foreach (var (label, value) in OfficeModeChoices)
        {
            OfficeModeCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectComboByTag(OfficeModeCombo, settings.OfficeInputMode, WpsInputModeDefaults.Auto);
        OfficeModeCombo.ToolTip = "仅影响 Microsoft PowerPoint（PPT）放映控制。";

        foreach (var (label, value) in WpsModeChoices)
        {
            WpsModeCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectComboByTag(WpsModeCombo, settings.WpsInputMode, WpsInputModeDefaults.Auto);
        _suppressPresetSelectionChanged = true;

        foreach (var (label, value) in PresetSchemeChoices)
        {
            PresetSchemeCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        var initialPreset = ResolveInitialPresetScheme(settings);
        SelectComboByTag(PresetSchemeCombo, initialPreset, PresetSchemeDefaults.Custom);
        UpdatePresetHint(initialPreset);
        _currentPresetScheme = initialPreset;
        _suppressPresetSelectionChanged = false;

        foreach (var (label, value) in WpsDebounceChoices)
        {
            WpsDebounceCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        EnsureIntComboOption(
            WpsDebounceCombo,
            settings.WpsDebounceMs,
            string.Create(CultureInfo.InvariantCulture, $"自定义（{settings.WpsDebounceMs} ms）"));
        SelectIntCombo(WpsDebounceCombo, settings.WpsDebounceMs, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);

        foreach (var (label, value) in FallbackFailureThresholdChoices)
        {
            FallbackFailureThresholdCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        EnsureIntComboOption(
            FallbackFailureThresholdCombo,
            settings.PresentationAutoFallbackFailureThreshold,
            string.Create(CultureInfo.InvariantCulture, $"自定义（{settings.PresentationAutoFallbackFailureThreshold} 次）"));
        SelectIntCombo(
            FallbackFailureThresholdCombo,
            settings.PresentationAutoFallbackFailureThreshold,
            fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault);

        foreach (var (label, value) in FallbackProbeIntervalChoices)
        {
            FallbackProbeIntervalCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        EnsureIntComboOption(
            FallbackProbeIntervalCombo,
            settings.PresentationAutoFallbackProbeIntervalCommands,
            string.Create(CultureInfo.InvariantCulture, $"自定义（{settings.PresentationAutoFallbackProbeIntervalCommands} 次）"));
        SelectIntCombo(
            FallbackProbeIntervalCombo,
            settings.PresentationAutoFallbackProbeIntervalCommands,
            fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault);

        WpsWheelCheck.IsChecked = settings.WpsWheelForward;
        LockStrategyOnDegradeCheck.IsChecked = settings.PresentationLockStrategyWhenDegraded;
        PresentationClassifierAutoLearnCheck.IsChecked = settings.PresentationClassifierAutoLearnEnabled;
        PresentationClassifierClearOverridesCheck.IsChecked = false;
        RefreshPresentationClassifierPackageStatusText(
            BuildClassifierPackageStatusFromOverrides(
                _workingPresentationClassifierOverridesJson,
                importedDetail: null));
        ForceForegroundCheck.IsChecked = settings.ForcePresentationForegroundOnFullscreen;
        InkSaveCheck.IsChecked = settings.InkSaveEnabled;

        foreach (var (label, scope) in InkExportScopeChoices)
        {
            InkExportScopeCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = scope });
        }
        SelectInkExportScope(settings.InkExportScope);

        foreach (var (label, value) in ExportParallelChoices)
        {
            ExportParallelCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectIntCombo(
            ExportParallelCombo,
            settings.InkExportMaxParallelFiles,
            fallback: PaintSettingsOptionDefaults.InkExportMaxParallelDefault);

        foreach (var (label, value) in NeighborPrefetchChoices)
        {
            NeighborPrefetchCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectIntCombo(
            NeighborPrefetchCombo,
            settings.PhotoNeighborPrefetchRadiusMax,
            fallback: PaintSettingsOptionDefaults.PhotoNeighborPrefetchRadiusDefault);

        foreach (var (label, value) in PostInputRefreshDelayChoices)
        {
            PostInputRefreshDelayCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        EnsureIntComboOption(
            PostInputRefreshDelayCombo,
            settings.PhotoPostInputRefreshDelayMs,
            string.Create(CultureInfo.InvariantCulture, $"自定义（{settings.PhotoPostInputRefreshDelayMs}ms）"));
        SelectIntCombo(PostInputRefreshDelayCombo, settings.PhotoPostInputRefreshDelayMs, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs);

        foreach (var (label, value) in WheelZoomBaseChoices)
        {
            WheelZoomBaseCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        EnsureDoubleComboOption(
            WheelZoomBaseCombo,
            settings.PhotoWheelZoomBase,
            string.Create(CultureInfo.InvariantCulture, $"自定义（{settings.PhotoWheelZoomBase:0.####}）"));
        SelectDoubleCombo(WheelZoomBaseCombo, settings.PhotoWheelZoomBase, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault);

        foreach (var (label, value) in GestureSensitivityChoices)
        {
            GestureSensitivityCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        EnsureDoubleComboOption(
            GestureSensitivityCombo,
            settings.PhotoGestureZoomSensitivity,
            string.Create(CultureInfo.InvariantCulture, $"自定义（{settings.PhotoGestureZoomSensitivity:0.###}x）"));
        SelectDoubleCombo(GestureSensitivityCombo, settings.PhotoGestureZoomSensitivity, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault);

        foreach (var (label, value) in PhotoInertiaProfileChoices)
        {
            PhotoInertiaProfileCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectComboByTag(
            PhotoInertiaProfileCombo,
            PhotoInertiaProfileDefaults.Normalize(settings.PhotoInertiaProfile),
            PhotoInertiaProfileDefaults.Standard);
        PhotoInputTelemetryCheck.IsChecked = settings.PhotoInputTelemetryEnabled;
        PhotoRememberTransformCheck.IsChecked = settings.PhotoRememberTransform;
        PhotoCrossPageDisplayCheck.IsChecked = settings.PhotoCrossPageDisplay;

        foreach (var (label, style) in BrushStyleChoices)
        {
            var item = new WpfComboBoxItem { Content = label, Tag = style };
            BrushStyleCombo.Items.Add(item);
        }
        SelectBrushStyle(settings.BrushStyle);

        foreach (var (label, preset) in WhiteboardPresetChoices)
        {
            WhiteboardPresetCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = preset });
        }
        SelectWhiteboardPreset(settings.WhiteboardPreset);

        foreach (var (label, preset) in CalligraphyPresetChoices)
        {
            CalligraphyPresetCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = preset });
        }
        foreach (var (label, mode) in ClassroomWritingModeChoices)
        {
            ClassroomWritingModeCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = mode });
        }
        ClassroomWritingModeCombo.SelectionChanged += OnClassroomWritingModeChanged;
        SelectClassroomWritingMode(settings.ClassroomWritingMode);
        UpdateClassroomWritingModeHint(settings.ClassroomWritingMode);
        SelectCalligraphyPreset(settings.CalligraphyPreset);
        CalligraphyInkBloomCheck.IsChecked = settings.CalligraphyInkBloomEnabled;
        CalligraphySealCheck.IsChecked = settings.CalligraphySealEnabled;
        UpdateCalligraphyOptionState();

        BrushSizeSlider.Value = Clamp(settings.BrushSize, 1, 50);
        EraserSizeSlider.Value = Clamp(settings.EraserSize, 6, 60);
        BrushOpacitySlider.Value = ToPercent(settings.BrushOpacity);
        CalligraphyOverlayThresholdSlider.Value = ToPercent(settings.CalligraphyOverlayOpacityThreshold);

        foreach (var (label, type) in ShapeChoices)
        {
            var item = new WpfComboBoxItem { Content = label, Tag = type };
            ShapeCombo.Items.Add(item);
        }
        SelectShapeType(settings.ShapeType);

        foreach (var scale in ToolbarScaleChoices)
        {
            var percent = (int)Math.Round(scale * 100);
            ToolbarScaleCombo.Items.Add(new WpfComboBoxItem { Content = $"{percent}%", Tag = scale });
        }
        var selectedScale = FindNearestScale(settings.PaintToolbarScale);
        SelectComboByTag(ToolbarScaleCombo, selectedScale);

        UpdateBrushSizeLabel();
        UpdateBrushOpacityLabel();
        UpdateEraserSizeLabel();
        UpdateCalligraphyOverlayThresholdLabel();
        Loaded += OnDialogLoaded;
        Closed += OnDialogClosed;

        InitializeCustomSnapshotIfNeeded();
        AttachPresetManagedControlHandlers();
        AttachSectionDirtyTrackingHandlers();
        _suppressPresetAutoCustom = false;
        UpdatePresetHint(_currentPresetScheme);
        _advancedModeEnabled = false;
        if (AdvancedModeCheck != null)
        {
            AdvancedModeCheck.IsChecked = _advancedModeEnabled;
        }
        UpdateAdvancedModeVisibility();
        ApplySceneCardsLayout(SceneCardsGrid?.ActualWidth ?? 0);
        _initialPresetBrushSectionState = CapturePresetBrushSectionStateFromControls();
        _initialSceneSectionState = CaptureSceneSectionStateFromControls();
        _initialAdvancedSectionState = CaptureAdvancedSectionStateFromControls();
        _suppressSectionDirtyTracking = false;
        UpdateSectionDirtyStates();
    }
}
