using System;
using System.Windows;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog
{
    private PresetBrushSectionState CapturePresetBrushSectionStateFromControls()
    {
        return new PresetBrushSectionState(
            PresetScheme: GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom),
            BrushColor: BrushColor,
            BoardColor: BoardColor,
            QuickColor1: QuickColor1,
            QuickColor2: QuickColor2,
            QuickColor3: QuickColor3,
            BrushStyle: ResolveBrushStyle(),
            WhiteboardPreset: ResolveWhiteboardPreset(),
            CalligraphyPreset: ResolveCalligraphyPreset(),
            ClassroomWritingMode: ResolveClassroomWritingMode(),
            QuickBrushSize1Px: (int)Math.Round(BrushSizeSlider.Value),
            QuickBrushSize2Px: (int)Math.Round(BrushSize2Slider.Value),
            QuickBrushSize3Px: (int)Math.Round(BrushSize3Slider.Value),
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
            ControlMsPpt: ControlMsPptCheck.IsChecked == true,
            ControlWpsPpt: ControlWpsPptCheck.IsChecked == true,
            InkCacheEnabled: InkCacheCheck.IsChecked == true,
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

}
