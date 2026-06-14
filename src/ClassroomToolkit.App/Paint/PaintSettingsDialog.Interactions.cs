using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using MediaColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog : Window
{
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
        QuickBrushSize1 = Clamp(BrushSizeSlider.Value, 1, 50);
        QuickBrushSize2 = Clamp(BrushSize2Slider.Value, 1, 50);
        QuickBrushSize3 = Clamp(BrushSize3Slider.Value, 1, 50);
        BrushSize = ResolveActiveBrushSize();
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

    private double ResolveActiveBrushSize()
    {
        if (IsSameRgb(BrushColor, QuickColor1))
        {
            return QuickBrushSize1;
        }

        if (IsSameRgb(BrushColor, QuickColor2))
        {
            return QuickBrushSize2;
        }

        if (IsSameRgb(BrushColor, QuickColor3))
        {
            return QuickBrushSize3;
        }

        return Clamp(_initialBrushSize, 1, 50);
    }

    private static bool IsSameRgb(MediaColor left, MediaColor right)
    {
        return left.R == right.R && left.G == right.G && left.B == right.B;
    }

    private void OnBrushStyleChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateCalligraphyOptionState();
    }


}
