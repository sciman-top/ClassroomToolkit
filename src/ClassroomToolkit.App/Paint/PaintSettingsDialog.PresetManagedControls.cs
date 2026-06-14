using System;
using System.Windows;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void OnClassroomWritingModeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateClassroomWritingModeHint(ResolveClassroomWritingMode());
        DemotePresetToCustomWhenManuallyOverridden();
    }

    private void OnPresetManagedComboChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        DemotePresetToCustomWhenManuallyOverridden();
    }

    private void OnPresetManagedToggleChanged(object sender, RoutedEventArgs e)
    {
        DemotePresetToCustomWhenManuallyOverridden();
    }

    private void AttachPresetManagedControlHandlers()
    {
        WpsModeCombo.SelectionChanged += OnPresetManagedComboChanged;
        FallbackFailureThresholdCombo.SelectionChanged += OnPresetManagedComboChanged;
        FallbackProbeIntervalCombo.SelectionChanged += OnPresetManagedComboChanged;
        WpsDebounceCombo.SelectionChanged += OnPresetManagedComboChanged;
        PostInputRefreshDelayCombo.SelectionChanged += OnPresetManagedComboChanged;
        WheelZoomBaseCombo.SelectionChanged += OnPresetManagedComboChanged;
        GestureSensitivityCombo.SelectionChanged += OnPresetManagedComboChanged;
        PhotoInertiaProfileCombo.SelectionChanged += OnPresetManagedComboChanged;
        WpsWheelCheck.Checked += OnPresetManagedToggleChanged;
        WpsWheelCheck.Unchecked += OnPresetManagedToggleChanged;
        LockStrategyOnDegradeCheck.Checked += OnPresetManagedToggleChanged;
        LockStrategyOnDegradeCheck.Unchecked += OnPresetManagedToggleChanged;
    }

    private void DetachPresetManagedControlHandlers()
    {
        WpsModeCombo.SelectionChanged -= OnPresetManagedComboChanged;
        FallbackFailureThresholdCombo.SelectionChanged -= OnPresetManagedComboChanged;
        FallbackProbeIntervalCombo.SelectionChanged -= OnPresetManagedComboChanged;
        WpsDebounceCombo.SelectionChanged -= OnPresetManagedComboChanged;
        PostInputRefreshDelayCombo.SelectionChanged -= OnPresetManagedComboChanged;
        WheelZoomBaseCombo.SelectionChanged -= OnPresetManagedComboChanged;
        GestureSensitivityCombo.SelectionChanged -= OnPresetManagedComboChanged;
        PhotoInertiaProfileCombo.SelectionChanged -= OnPresetManagedComboChanged;
        WpsWheelCheck.Checked -= OnPresetManagedToggleChanged;
        WpsWheelCheck.Unchecked -= OnPresetManagedToggleChanged;
        LockStrategyOnDegradeCheck.Checked -= OnPresetManagedToggleChanged;
        LockStrategyOnDegradeCheck.Unchecked -= OnPresetManagedToggleChanged;
    }

    private void DemotePresetToCustomWhenManuallyOverridden()
    {
        if (_suppressPresetSelectionChanged || _suppressPresetAutoCustom)
        {
            return;
        }
        if (PresetSchemeCombo == null)
        {
            return;
        }
        var preset = GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom);
        if (string.Equals(preset, PresetSchemeDefaults.Custom, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SaveCurrentAsCustomSnapshot();
        _suppressPresetSelectionChanged = true;
        try
        {
            SelectComboByTag(PresetSchemeCombo, PresetSchemeDefaults.Custom, PresetSchemeDefaults.Custom);
        }
        finally
        {
            _suppressPresetSelectionChanged = false;
        }
        _currentPresetScheme = PresetSchemeDefaults.Custom;
        UpdatePresetHint(PresetSchemeDefaults.Custom);
    }

    private void UpdateManagedControlVisualState(string preset)
    {
        var isCustom = IsCustomScheme(preset);
        var tip = isCustom
            ? "WPS 策略（仅影响 WPS）：自定义模式下可独立调整。"
            : "WPS 策略（仅影响 WPS）：当前为预设模式，切换到“自定义”后可独立调整。";

        WpsModeCombo.ToolTip = tip;
        WpsDebounceCombo.ToolTip = tip;
        WpsWheelCheck.ToolTip = tip;
        LockStrategyOnDegradeCheck.ToolTip = tip;
        FallbackFailureThresholdCombo.ToolTip = tip;
        FallbackProbeIntervalCombo.ToolTip = tip;
        PostInputRefreshDelayCombo.ToolTip = tip;
        WheelZoomBaseCombo.ToolTip = tip;
        GestureSensitivityCombo.ToolTip = tip;
        PhotoInertiaProfileCombo.ToolTip = tip;
        ClassroomWritingModeCombo.ToolTip = tip;
        WpsModeCombo.IsEnabled = isCustom;
        WpsDebounceCombo.IsEnabled = isCustom;
        WpsWheelCheck.IsEnabled = isCustom;
        LockStrategyOnDegradeCheck.IsEnabled = isCustom;
        FallbackFailureThresholdCombo.IsEnabled = isCustom;
        FallbackProbeIntervalCombo.IsEnabled = isCustom;
        PostInputRefreshDelayCombo.IsEnabled = isCustom;
        WheelZoomBaseCombo.IsEnabled = isCustom;
        GestureSensitivityCombo.IsEnabled = isCustom;
        PhotoInertiaProfileCombo.IsEnabled = isCustom;
        ClassroomWritingModeCombo.IsEnabled = isCustom;
        if (ConvertToCustomEditingButton != null)
        {
            ConvertToCustomEditingButton.Visibility = isCustom ? Visibility.Collapsed : Visibility.Visible;
            ConvertToCustomEditingButton.IsEnabled = !isCustom;
        }
    }
}
