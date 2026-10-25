using System.Windows;

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
        BrushSize2Slider.ValueChanged += OnSectionDirtyValueChanged;
        BrushSize3Slider.ValueChanged += OnSectionDirtyValueChanged;
        BrushOpacitySlider.ValueChanged += OnSectionDirtyValueChanged;
        EraserSizeSlider.ValueChanged += OnSectionDirtyValueChanged;
        CalligraphyOverlayThresholdSlider.ValueChanged += OnSectionDirtyValueChanged;

        CalligraphyInkBloomCheck.Checked += OnSectionDirtyRoutedChanged;
        CalligraphyInkBloomCheck.Unchecked += OnSectionDirtyRoutedChanged;
        CalligraphySealCheck.Checked += OnSectionDirtyRoutedChanged;
        CalligraphySealCheck.Unchecked += OnSectionDirtyRoutedChanged;
        InkSaveCheck.Checked += OnSectionDirtyRoutedChanged;
        InkSaveCheck.Unchecked += OnSectionDirtyRoutedChanged;
        InkCacheCheck.Checked += OnSectionDirtyRoutedChanged;
        InkCacheCheck.Unchecked += OnSectionDirtyRoutedChanged;
        ControlMsPptCheck.Checked += OnSectionDirtyRoutedChanged;
        ControlMsPptCheck.Unchecked += OnSectionDirtyRoutedChanged;
        ControlWpsPptCheck.Checked += OnSectionDirtyRoutedChanged;
        ControlWpsPptCheck.Unchecked += OnSectionDirtyRoutedChanged;
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
        BrushSize2Slider.ValueChanged -= OnSectionDirtyValueChanged;
        BrushSize3Slider.ValueChanged -= OnSectionDirtyValueChanged;
        BrushOpacitySlider.ValueChanged -= OnSectionDirtyValueChanged;
        EraserSizeSlider.ValueChanged -= OnSectionDirtyValueChanged;
        CalligraphyOverlayThresholdSlider.ValueChanged -= OnSectionDirtyValueChanged;

        CalligraphyInkBloomCheck.Checked -= OnSectionDirtyRoutedChanged;
        CalligraphyInkBloomCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        CalligraphySealCheck.Checked -= OnSectionDirtyRoutedChanged;
        CalligraphySealCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        InkSaveCheck.Checked -= OnSectionDirtyRoutedChanged;
        InkSaveCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        InkCacheCheck.Checked -= OnSectionDirtyRoutedChanged;
        InkCacheCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        ControlMsPptCheck.Checked -= OnSectionDirtyRoutedChanged;
        ControlMsPptCheck.Unchecked -= OnSectionDirtyRoutedChanged;
        ControlWpsPptCheck.Checked -= OnSectionDirtyRoutedChanged;
        ControlWpsPptCheck.Unchecked -= OnSectionDirtyRoutedChanged;
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
}
