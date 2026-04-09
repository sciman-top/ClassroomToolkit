using System;
using System.Windows;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private void OnPresetSchemeChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_suppressPresetSelectionChanged)
        {
            return;
        }
        var preset = GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom);
        if (IsCustomScheme(_currentPresetScheme) && !IsCustomScheme(preset))
        {
            SaveCurrentAsCustomSnapshot();
        }
        UpdatePresetHint(preset);
        ApplyPresetScheme(preset);
        _currentPresetScheme = preset;
    }

    private void OnConvertToCustomEditingClick(object sender, RoutedEventArgs e)
    {
        var preset = GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom);
        if (IsCustomScheme(preset))
        {
            return;
        }

        SaveCurrentAsCustomSnapshot();
        SelectComboByTag(PresetSchemeCombo, PresetSchemeDefaults.Custom, PresetSchemeDefaults.Custom);
    }

    private void ApplyPresetScheme(string preset)
    {
        if (!PresetSchemePolicy.TryResolveManagedParameters(preset, out var parameters))
        {
            return;
        }

        var before = CaptureManagedParametersFromControls();
        _suppressPresetAutoCustom = true;
        try
        {
            ApplyManagedParametersToControls(parameters);
        }
        finally
        {
            _suppressPresetAutoCustom = false;
        }
        UpdateClassroomWritingModeHint(parameters.ClassroomWritingMode);
        System.Diagnostics.Debug.WriteLine(
            $"[PaintPreset] apply {preset}: before=({FormatManagedParameters(before)}) -> after=({FormatManagedParameters(parameters)})");
    }

    private static string ResolveInitialPresetScheme(AppSettings settings)
    {
        return PresetSchemePolicy.ResolveInitialScheme(settings);
    }

    private void UpdatePresetHint(string preset)
    {
        if (PresetSchemeHintText == null)
        {
            return;
        }
        if (!PresetHints.TryGetValue(preset, out var hint))
        {
            hint = PresetHints[PresetSchemeDefaults.Custom];
        }
        PresetSchemeHintText.Text = hint;
        PresetSchemeHintText.Visibility = string.IsNullOrWhiteSpace(hint)
            ? Visibility.Collapsed
            : Visibility.Visible;
        if (PresetManagedHintText != null)
        {
            var isCustom = IsCustomScheme(preset);
            var managedHint = isCustom
                ? PresetManagedHintForCustom
                : PresetManagedHintForPreset;
            PresetManagedHintText.Text = managedHint;
            PresetManagedHintText.Visibility = string.IsNullOrWhiteSpace(managedHint)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
        UpdateManagedControlVisualState(preset);
        UpdatePresetRecommendation(preset);
    }

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

    private static bool IsCustomScheme(string preset)
    {
        return string.Equals(preset, PresetSchemeDefaults.Custom, StringComparison.OrdinalIgnoreCase);
    }

    private void InitializeCustomSnapshotIfNeeded()
    {
        if (IsCustomScheme(_currentPresetScheme))
        {
            SaveCurrentAsCustomSnapshot();
        }
    }

    private void SaveCurrentAsCustomSnapshot()
    {
        _customManagedSnapshot = CaptureManagedParametersFromControls();
        System.Diagnostics.Debug.WriteLine($"[PaintPreset] save custom snapshot: {FormatManagedParameters(_customManagedSnapshot)}");
    }

    private PresetSchemeManagedParameters CaptureManagedParametersFromControls()
    {
        return new PresetSchemeManagedParameters(
            GetSelectedTag(WpsModeCombo, WpsInputModeDefaults.Auto),
            WpsWheelCheck.IsChecked == true,
            LockStrategyOnDegradeCheck.IsChecked != false,
            ResolveIntCombo(
                FallbackFailureThresholdCombo,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault),
            ResolveIntCombo(
                FallbackProbeIntervalCombo,
                fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault),
            ResolveClassroomWritingMode(),
            ResolveIntCombo(WpsDebounceCombo, fallback: PaintPresetDefaults.WpsDebounceDefaultMs),
            ResolveIntCombo(PostInputRefreshDelayCombo, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs),
            ResolveDoubleCombo(WheelZoomBaseCombo, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault),
            ResolveDoubleCombo(GestureSensitivityCombo, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault),
            PhotoInertiaProfileDefaults.Normalize(GetSelectedTag(PhotoInertiaProfileCombo, PhotoInertiaProfileDefaults.Standard)));
    }

    private void ApplyManagedParametersToControls(PresetSchemeManagedParameters parameters)
    {
        SelectComboByTag(WpsModeCombo, parameters.WpsInputMode, WpsInputModeDefaults.Auto);
        LockStrategyOnDegradeCheck.IsChecked = parameters.LockStrategyWhenDegraded;
        WpsWheelCheck.IsChecked = parameters.WpsWheelForward;
        SelectIntCombo(
            FallbackFailureThresholdCombo,
            parameters.AutoFallbackFailureThreshold,
            fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault);
        SelectIntCombo(
            FallbackProbeIntervalCombo,
            parameters.AutoFallbackProbeIntervalCommands,
            fallback: ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault);
        SelectClassroomWritingMode(parameters.ClassroomWritingMode);
        SelectIntCombo(WpsDebounceCombo, parameters.WpsDebounceMs, fallback: parameters.WpsDebounceMs);
        SelectIntCombo(PostInputRefreshDelayCombo, parameters.PhotoPostInputRefreshDelayMs, fallback: parameters.PhotoPostInputRefreshDelayMs);
        SelectDoubleCombo(WheelZoomBaseCombo, parameters.PhotoWheelZoomBase, fallback: parameters.PhotoWheelZoomBase);
        SelectDoubleCombo(
            GestureSensitivityCombo,
            parameters.PhotoGestureZoomSensitivity,
            fallback: parameters.PhotoGestureZoomSensitivity);
        SelectComboByTag(
            PhotoInertiaProfileCombo,
            parameters.PhotoInertiaProfile,
            PhotoInertiaProfileDefaults.Standard);
    }

    private static string FormatManagedParameters(PresetSchemeManagedParameters parameters)
    {
        return $"mode={parameters.WpsInputMode}; wheel={parameters.WpsWheelForward}; lock={parameters.LockStrategyWhenDegraded}; " +
               $"fallbackFail={parameters.AutoFallbackFailureThreshold}; fallbackProbe={parameters.AutoFallbackProbeIntervalCommands}; " +
               $"writing={parameters.ClassroomWritingMode}; debounce={parameters.WpsDebounceMs}; postInput={parameters.PhotoPostInputRefreshDelayMs}; " +
               $"wheelZoom={parameters.PhotoWheelZoomBase:0.####}; gesture={parameters.PhotoGestureZoomSensitivity:0.###}; " +
               $"inertia={parameters.PhotoInertiaProfile}";
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
