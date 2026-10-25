using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
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
}
