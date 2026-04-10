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
    private static readonly (string Label, PaintShapeType Type)[] ShapeChoices =
    {
        ("无", PaintShapeType.None),
        ("实直线", PaintShapeType.Line),
        ("虚直线", PaintShapeType.DashedLine),
        ("实箭头", PaintShapeType.Arrow),
        ("虚箭头", PaintShapeType.DashedArrow),
        ("空心矩形", PaintShapeType.Rectangle),
        ("实心矩形", PaintShapeType.RectangleFill),
        ("圆形/椭圆", PaintShapeType.Ellipse),
        ("任意三角形", PaintShapeType.Triangle)
    };
    private static readonly (string Label, PaintBrushStyle Style)[] BrushStyleChoices =
    {
        ("白板笔", PaintBrushStyle.StandardRibbon),
        ("毛笔", PaintBrushStyle.Calligraphy)
    };
    private static readonly (string Label, WhiteboardBrushPreset Preset)[] WhiteboardPresetChoices =
    {
        ("顺滑", WhiteboardBrushPreset.Smooth),
        ("平衡", WhiteboardBrushPreset.Balanced),
        ("锋利", WhiteboardBrushPreset.Sharp)
    };
    private static readonly (string Label, CalligraphyBrushPreset Preset)[] CalligraphyPresetChoices =
    {
        ("板书清晰（锋利）", CalligraphyBrushPreset.Sharp),
        ("板书清晰（平衡）", CalligraphyBrushPreset.Balanced),
        ("毛笔感（更明显）", CalligraphyBrushPreset.Soft)
    };
    private static readonly (string Label, ClassroomWritingMode Mode)[] ClassroomWritingModeChoices =
    {
        ("稳笔模式（老机优先）", ClassroomWritingMode.Stable),
        ("课堂通用（推荐）", ClassroomWritingMode.Balanced),
        ("跟手优先（新机）", ClassroomWritingMode.Responsive)
    };
    private static readonly Dictionary<ClassroomWritingMode, string> ClassroomWritingModeHints = new()
    {
        [ClassroomWritingMode.Stable] = "适合老旧一体机或干扰较多的教室，笔迹更稳、更抗抖，但连续书写会稍慢。",
        [ClassroomWritingMode.Balanced] = "课堂默认推荐：稳和快比较均衡，大多数触屏一体机可直接用。",
        [ClassroomWritingMode.Responsive] = "适合较新高性能一体机，跟手更快，但轻微抖动也更容易显现。"
    };
    private static readonly double[] ToolbarScaleChoices =
    {
        ToolbarScaleDefaults.Min,
        ToolbarScaleDefaults.Default,
        1.25,
        1.5,
        1.75,
        ToolbarScaleDefaults.Max
    };
    private static readonly (string Label, InkExportScope Scope)[] InkExportScopeChoices =
    {
        ("全部历史与本次", InkExportScope.AllPersistedAndSession),
        ("仅本次新增/修改", InkExportScope.SessionChangesOnly)
    };
    private static readonly (string Label, int Value)[] ExportParallelChoices =
    {
        ("自动", 0),
        ("1", 1),
        ("2", 2),
        ("3", 3),
        ("4", 4)
    };
    private static readonly (string Label, int Value)[] NeighborPrefetchChoices =
    {
        ("低", 2),
        ("中", 3),
        ("高", 4)
    };
    private static readonly (string Label, int Value)[] PostInputRefreshDelayChoices =
    {
        ("更快（80ms）", PaintPresetDefaults.PostInputResponsiveMs),
        ("平衡（120ms，推荐/默认）", PaintPresetDefaults.PostInputBalancedMs),
        ("稳定（140ms）", PaintPresetDefaults.PostInputStableMs),
        ("稳态增强（160ms）", PaintPresetDefaults.PostInputDualScreenMs),
        ("保守（180ms）", 180)
    };
    private static readonly (string Label, double Value)[] WheelZoomBaseChoices =
    {
        ("细腻（慢）", PaintPresetDefaults.WheelZoomStable),
        ("平稳（中慢）", PaintPresetDefaults.WheelZoomDualScreen),
        ("平衡（推荐/默认）", PaintPresetDefaults.WheelZoomBalanced),
        ("迅速（快）", PaintPresetDefaults.WheelZoomResponsive)
    };
    private static readonly (string Label, double Value)[] GestureSensitivityChoices =
    {
        ("柔和（0.8x）", PaintPresetDefaults.GestureSensitivityStable),
        ("平稳（0.9x）", PaintPresetDefaults.GestureSensitivityDualScreen),
        ("标准（1.0x，推荐/默认）", PhotoZoomInputDefaults.GestureSensitivityDefault),
        ("灵敏（1.2x）", PaintPresetDefaults.GestureSensitivityResponsive)
    };
    private static readonly (string Label, string Value)[] PhotoInertiaProfileChoices =
    {
        ("标准（推荐）", PhotoInertiaProfileDefaults.Standard),
        ("灵敏（轻甩易触发）", PhotoInertiaProfileDefaults.Sensitive),
        ("重惯性（甩动更远）", PhotoInertiaProfileDefaults.Heavy)
    };
    private static readonly (string Label, string Value)[] PresetSchemeChoices =
    {
        ("自定义（高级）", PresetSchemeDefaults.Custom),
        ("课堂平衡（推荐）", PresetSchemeDefaults.Balanced),
        ("高灵敏（新设备）", PresetSchemeDefaults.Responsive),
        ("高稳定（老设备）", PresetSchemeDefaults.Stable)
    };
    private static readonly Dictionary<string, string> PresetHints = new(StringComparer.OrdinalIgnoreCase)
    {
        [PresetSchemeDefaults.Custom] = string.Empty,
        [PresetSchemeDefaults.Balanced] = "课堂通用推荐：稳和快均衡，大多数设备可直接使用。",
        [PresetSchemeDefaults.Responsive] = "高灵敏（新设备）：跟手更快，适合高性能一体机。",
        [PresetSchemeDefaults.Stable] = "高稳定（老设备）：抗抖和容错更强，适合老旧设备或复杂环境。"
    };
    private const string PresetManagedHintForCustom = "";
    private const string PresetManagedHintForPreset =
        "当前为预设模式：部分参数由预设统一管理。点击「切换为自定义」后可单独调整。";

    private readonly record struct PresetBrushSectionState(
        string PresetScheme,
        PaintBrushStyle BrushStyle,
        WhiteboardBrushPreset WhiteboardPreset,
        CalligraphyBrushPreset CalligraphyPreset,
        ClassroomWritingMode ClassroomWritingMode,
        int BrushSizePx,
        int BrushOpacityPercent,
        int EraserSizePx,
        bool CalligraphyInkBloomEnabled,
        bool CalligraphySealEnabled,
        int CalligraphyOverlayThresholdPercent,
        string WpsInputMode,
        bool WpsWheelForward,
        bool LockStrategyWhenDegraded,
        int AutoFallbackFailureThreshold,
        int AutoFallbackProbeIntervalCommands,
        int WpsDebounceMs,
        int PhotoPostInputRefreshDelayMs,
        double PhotoWheelZoomBase,
        double PhotoGestureZoomSensitivity,
        string PhotoInertiaProfile);

    private readonly record struct SceneSectionState(
        bool InkSaveEnabled,
        InkExportScope InkExportScope,
        int InkExportMaxParallelFiles,
        bool PhotoCrossPageDisplay,
        bool PhotoRememberTransform,
        bool PhotoInputTelemetryEnabled,
        int PhotoNeighborPrefetchRadiusMax,
        int PhotoPostInputRefreshDelayMs,
        double PhotoWheelZoomBase,
        double PhotoGestureZoomSensitivity,
        string PhotoInertiaProfile,
        string OfficeInputMode,
        string WpsInputMode,
        bool WpsWheelForward,
        bool ForcePresentationForegroundOnFullscreen,
        int WpsDebounceMs,
        bool LockStrategyWhenDegraded,
        int PresentationAutoFallbackFailureThreshold,
        int PresentationAutoFallbackProbeIntervalCommands,
        bool PresentationClassifierAutoLearnEnabled,
        bool PresentationClassifierClearOverridesRequested,
        string PresentationClassifierOverridesJson);

    private readonly record struct AdvancedSectionState(
        PaintShapeType ShapeType,
        double ToolbarScale);

    public bool ControlMsPpt { get; private set; }
    public bool ControlWpsPpt { get; private set; }
    public string OfficeInputMode { get; private set; } = WpsInputModeDefaults.Auto;
    public string WpsInputMode { get; private set; } = WpsInputModeDefaults.Auto;
    public bool WpsWheelForward { get; private set; }
    public int WpsDebounceMs { get; private set; } = PaintPresetDefaults.WpsDebounceDefaultMs;
    public bool PresentationLockStrategyWhenDegraded { get; private set; } = true;
    public int PresentationAutoFallbackFailureThreshold { get; private set; } =
        ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackFailureThresholdDefault;
    public int PresentationAutoFallbackProbeIntervalCommands { get; private set; } =
        ClassroomToolkit.Services.Presentation.PresentationControlOptions.AutoFallbackProbeIntervalCommandsDefault;
    public bool PresentationClassifierAutoLearnEnabled { get; private set; }
    public bool PresentationClassifierClearOverridesRequested { get; private set; }
    public string PresentationClassifierOverridesJson { get; private set; } = string.Empty;
    public bool ForcePresentationForegroundOnFullscreen { get; private set; }
    public double BrushSize { get; private set; }
    public byte BrushOpacity { get; private set; }
    public PaintBrushStyle BrushStyle { get; private set; } = PaintBrushStyle.StandardRibbon;
    public WhiteboardBrushPreset WhiteboardPreset { get; private set; } = WhiteboardBrushPreset.Smooth;
    public CalligraphyBrushPreset CalligraphyPreset { get; private set; } = CalligraphyBrushPreset.Sharp;
    public string PresetScheme { get; private set; } = PresetSchemeDefaults.Custom;
    public ClassroomWritingMode ClassroomWritingMode { get; private set; } = ClassroomWritingMode.Balanced;
    public bool CalligraphyInkBloomEnabled { get; private set; }
    public bool CalligraphySealEnabled { get; private set; }
    public byte CalligraphyOverlayOpacityThreshold { get; private set; }
    public double EraserSize { get; private set; }
    public PaintShapeType ShapeType { get; private set; } = PaintShapeType.Line;
    public MediaColor BrushColor { get; private set; }
    public MediaColor QuickColor1 { get; private set; } = MediaColors.Black;
    public MediaColor QuickColor2 { get; private set; } = MediaColors.Red;
    public MediaColor QuickColor3 { get; private set; } = MediaColors.DodgerBlue;
    public double ToolbarScale { get; private set; } = ToolbarScaleDefaults.Default;
    public bool InkSaveEnabled { get; private set; }
    public InkExportScope InkExportScope { get; private set; } = InkExportScope.AllPersistedAndSession;
    public int InkExportMaxParallelFiles { get; private set; } = PaintSettingsOptionDefaults.InkExportMaxParallelDefault;
    public bool PhotoRememberTransform { get; private set; }
    public bool PhotoCrossPageDisplay { get; private set; }
    public bool PhotoInputTelemetryEnabled { get; private set; }
    public int PhotoNeighborPrefetchRadiusMax { get; private set; } = PaintSettingsOptionDefaults.PhotoNeighborPrefetchRadiusDefault;
    public int PhotoPostInputRefreshDelayMs { get; private set; } = PaintPresetDefaults.PostInputRefreshDefaultMs;
    public double PhotoWheelZoomBase { get; private set; } = PhotoZoomInputDefaults.WheelZoomBaseDefault;
    public double PhotoGestureZoomSensitivity { get; private set; } = PhotoZoomInputDefaults.GestureSensitivityDefault;
    public string PhotoInertiaProfile { get; private set; } = PhotoInertiaProfileDefaults.Standard;
    private bool _suppressPresetSelectionChanged;
    private bool _suppressPresetAutoCustom = true;
    private string _currentPresetScheme = PresetSchemeDefaults.Custom;
    private PresetSchemeManagedParameters _customManagedSnapshot;
    private bool _sizeToContentCommitted;
    private readonly PresetSchemeRecommendation _presetRecommendation;
    private bool _suppressSectionDirtyTracking = true;
    private bool _advancedModeEnabled;
    private string _workingPresentationClassifierOverridesJson = string.Empty;
    private PresetBrushSectionState _initialPresetBrushSectionState;
    private SceneSectionState _initialSceneSectionState;
    private AdvancedSectionState _initialAdvancedSectionState;
}
