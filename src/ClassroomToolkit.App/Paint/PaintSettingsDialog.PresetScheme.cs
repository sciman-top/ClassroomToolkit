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

    private static bool IsCustomScheme(string preset)
    {
        return string.Equals(preset, PresetSchemeDefaults.Custom, StringComparison.OrdinalIgnoreCase);
    }
}
