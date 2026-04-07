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
using WpfGrid = System.Windows.Controls.Grid;
using WpfTabControl = System.Windows.Controls.TabControl;
using WpfTabItem = System.Windows.Controls.TabItem;

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
        [ClassroomWritingMode.Stable] = "适合老旧一体机或干扰较多教室，笔迹更稳更抗抖，快速连写会稍慢。",
        [ClassroomWritingMode.Balanced] = "课堂默认推荐：稳和快比较均衡，大多数触屏一体机可直接使用。",
        [ClassroomWritingMode.Responsive] = "适合较新高性能一体机，跟手更快，轻微抖动会更容易显现。"
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
        [PresetSchemeDefaults.Balanced] = "课堂通用推荐：大多数设备可直接使用，稳和快均衡。",
        [PresetSchemeDefaults.Responsive] = "高灵敏（新设备）：跟手更快，适合高性能一体机。",
        [PresetSchemeDefaults.Stable] = "高稳定（老设备）：抗抖与容错更强，适合老旧设备或复杂环境。"
    };
    private const string PresetManagedHintForCustom = "";
    private const string PresetManagedHintForPreset =
        "当前为预设模式：部分参数由预设统一管理。点击「切换为自定义后编辑」可单独调整。";

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

    public PaintSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 24);
        MaxWidth = Math.Max(560, SystemParameters.WorkArea.Width - 24);
        
        // 在构造函数中立即修复 BorderBrush 问题
        SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                BorderFixHelper.FixAllBorders(this);
                System.Diagnostics.Debug.WriteLine("PaintSettingsDialog: 构造函数中修复完成");
            },
            ex => System.Diagnostics.Debug.WriteLine($"PaintSettingsDialog 构造函数修复失败: {ex.Message}"));

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
        _presetRecommendation = PresetSchemePolicy.ResolveRecommendation(settings);
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

    private void OnDialogLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
        if (_sizeToContentCommitted)
        {
            return;
        }
        if (!DispatcherInvokeAvailabilityPolicy.CanBeginInvoke(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            return;
        }

        var scheduled = PaintActionInvoker.TryInvoke(() =>
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    _sizeToContentCommitted = true;
                    SizeToContent = System.Windows.SizeToContent.Manual;
                },
                System.Windows.Threading.DispatcherPriority.ContextIdle);
            return true;
        }, fallback: false);
        if (!scheduled)
        {
            if (Dispatcher.CheckAccess())
            {
                _sizeToContentCommitted = true;
                SizeToContent = System.Windows.SizeToContent.Manual;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[PaintSettingsDialog] deferred SizeToContent scheduling skipped");
            }
        }
    }

    private void OnDialogClosed(object? sender, EventArgs e)
    {
        DetachSectionDirtyTrackingHandlers();
        DetachPresetManagedControlHandlers();
        ClassroomWritingModeCombo.SelectionChanged -= OnClassroomWritingModeChanged;
        Loaded -= OnDialogLoaded;
        Closed -= OnDialogClosed;
    }

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
        BrushSize = Clamp(BrushSizeSlider.Value, 1, 50);
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

    private void OnRestoreDefaultsClick(object sender, RoutedEventArgs e)
    {
        if ((SettingsTabControl?.SelectedIndex ?? 0) == 0)
        {
            var result = System.Windows.MessageBox.Show(
                "重置“笔触与预设”会同时恢复部分场景参数（如 WPS 策略、抬笔后刷新、缩放灵敏度）。是否继续？",
                "仅重置当前页",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Question);
            if (result != System.Windows.MessageBoxResult.Yes)
            {
                return;
            }
        }

        ApplyDefaultSettingsForCurrentTab();
    }

    private void OnRestoreAllDefaultsClick(object sender, RoutedEventArgs e)
    {
        var result = System.Windows.MessageBox.Show(
            "将恢复画笔设置窗口中的全部默认参数，是否继续？",
            "重置全部设置",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        ApplyDefaultSettings();
    }

    private void ApplyDefaultSettingsForCurrentTab()
    {
        var defaults = new AppSettings();
        var tabIndex = SettingsTabControl?.SelectedIndex ?? 0;
        var defaultPreset = ResolveInitialPresetScheme(defaults);

        _suppressPresetSelectionChanged = true;
        _suppressPresetAutoCustom = true;
        _suppressSectionDirtyTracking = true;
        try
        {
            switch (tabIndex)
            {
                case 0:
                    SelectComboByTag(PresetSchemeCombo, defaultPreset, PresetSchemeDefaults.Custom);
                    _currentPresetScheme = defaultPreset;
                    if (!IsCustomScheme(defaultPreset))
                    {
                        // Keep preset selection and managed parameters consistent.
                        ApplyPresetScheme(defaultPreset);
                    }
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
                    break;
                case 1:
                    SelectShapeType(defaults.ShapeType);
                    SelectComboByTag(ToolbarScaleCombo, FindNearestScale(defaults.PaintToolbarScale));
                    break;
                case 2:
                    SelectComboByTag(OfficeModeCombo, defaults.OfficeInputMode, WpsInputModeDefaults.Auto);
                    SelectComboByTag(WpsModeCombo, defaults.WpsInputMode, WpsInputModeDefaults.Auto);
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
                    break;
                default:
                    ApplyDefaultSettings();
                    return;
            }
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
        UpdateClassroomWritingModeHint(ResolveClassroomWritingMode());
        if (tabIndex == 0 && IsCustomScheme(defaultPreset))
        {
            SaveCurrentAsCustomSnapshot();
        }

        UpdatePresetHint(GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom));
        ApplySceneCardsLayout(SceneCardsGrid?.ActualWidth ?? 0);
        UpdateSectionDirtyStates();
    }

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

    private void OnBrushStyleChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        UpdateCalligraphyOptionState();
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
            ? "WPS策略（仅影响 WPS）：自定义模式下可独立调整。"
            : "WPS策略（仅影响 WPS）：当前为预设模式，切换到“自定义”后可独立调整。";

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

    private void UpdateSectionDirtyStates()
    {
        if (_suppressSectionDirtyTracking) return;
        SetTabHeader(SettingsTabControl, 0, "基础", IsPresetBrushSectionDirty());
        SetTabHeader(SettingsTabControl, 1, "工具栏", IsAdvancedSectionDirty());
        SetTabHeader(SettingsTabControl, 2, "兼容", IsSceneSectionDirty());
        UpdateChangeSummaryText();
    }

    private bool IsPresetBrushSectionDirty()
    {
        var current = CapturePresetBrushSectionStateFromControls();
        var initial = _initialPresetBrushSectionState;
        return !string.Equals(current.PresetScheme, initial.PresetScheme, StringComparison.OrdinalIgnoreCase)
            || current.BrushStyle != initial.BrushStyle
            || current.WhiteboardPreset != initial.WhiteboardPreset
            || current.CalligraphyPreset != initial.CalligraphyPreset
            || current.ClassroomWritingMode != initial.ClassroomWritingMode
            || current.BrushSizePx != initial.BrushSizePx
            || current.BrushOpacityPercent != initial.BrushOpacityPercent
            || current.EraserSizePx != initial.EraserSizePx
            || current.CalligraphyInkBloomEnabled != initial.CalligraphyInkBloomEnabled
            || current.CalligraphySealEnabled != initial.CalligraphySealEnabled
            || current.CalligraphyOverlayThresholdPercent != initial.CalligraphyOverlayThresholdPercent
            || !string.Equals(current.WpsInputMode, initial.WpsInputMode, StringComparison.OrdinalIgnoreCase)
            || current.WpsWheelForward != initial.WpsWheelForward
            || current.LockStrategyWhenDegraded != initial.LockStrategyWhenDegraded
            || current.AutoFallbackFailureThreshold != initial.AutoFallbackFailureThreshold
            || current.AutoFallbackProbeIntervalCommands != initial.AutoFallbackProbeIntervalCommands
            || current.WpsDebounceMs != initial.WpsDebounceMs
            || current.PhotoPostInputRefreshDelayMs != initial.PhotoPostInputRefreshDelayMs
            || Math.Abs(current.PhotoWheelZoomBase - initial.PhotoWheelZoomBase) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || Math.Abs(current.PhotoGestureZoomSensitivity - initial.PhotoGestureZoomSensitivity) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || !string.Equals(current.PhotoInertiaProfile, initial.PhotoInertiaProfile, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSceneSectionDirty()
    {
        var current = CaptureSceneSectionStateFromControls();
        var initial = _initialSceneSectionState;
        return current.InkSaveEnabled != initial.InkSaveEnabled
            || current.InkExportScope != initial.InkExportScope
            || current.InkExportMaxParallelFiles != initial.InkExportMaxParallelFiles
            || current.PhotoCrossPageDisplay != initial.PhotoCrossPageDisplay
            || current.PhotoRememberTransform != initial.PhotoRememberTransform
            || current.PhotoInputTelemetryEnabled != initial.PhotoInputTelemetryEnabled
            || current.PhotoNeighborPrefetchRadiusMax != initial.PhotoNeighborPrefetchRadiusMax
            || current.PhotoPostInputRefreshDelayMs != initial.PhotoPostInputRefreshDelayMs
            || Math.Abs(current.PhotoWheelZoomBase - initial.PhotoWheelZoomBase) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || Math.Abs(current.PhotoGestureZoomSensitivity - initial.PhotoGestureZoomSensitivity) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || !string.Equals(current.PhotoInertiaProfile, initial.PhotoInertiaProfile, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.OfficeInputMode, initial.OfficeInputMode, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(current.WpsInputMode, initial.WpsInputMode, StringComparison.OrdinalIgnoreCase)
            || current.WpsWheelForward != initial.WpsWheelForward
            || current.ForcePresentationForegroundOnFullscreen != initial.ForcePresentationForegroundOnFullscreen
            || current.WpsDebounceMs != initial.WpsDebounceMs
            || current.LockStrategyWhenDegraded != initial.LockStrategyWhenDegraded
            || current.PresentationAutoFallbackFailureThreshold != initial.PresentationAutoFallbackFailureThreshold
            || current.PresentationAutoFallbackProbeIntervalCommands != initial.PresentationAutoFallbackProbeIntervalCommands
            || current.PresentationClassifierAutoLearnEnabled != initial.PresentationClassifierAutoLearnEnabled
            || current.PresentationClassifierClearOverridesRequested != initial.PresentationClassifierClearOverridesRequested
            || !string.Equals(
                NormalizePresentationClassifierOverridesJson(current.PresentationClassifierOverridesJson),
                NormalizePresentationClassifierOverridesJson(initial.PresentationClassifierOverridesJson),
                StringComparison.Ordinal);
    }

    private bool IsAdvancedSectionDirty()
    {
        var current = CaptureAdvancedSectionStateFromControls();
        var initial = _initialAdvancedSectionState;
        return current.ShapeType != initial.ShapeType
            || Math.Abs(current.ToolbarScale - initial.ToolbarScale) > PaintSettingsDefaults.ComboTagComparisonEpsilon;
    }

    private static void SetTabHeader(WpfTabControl? tabs, int index, string baseHeader, bool isDirty)
    {
        if (tabs == null || index < 0 || index >= tabs.Items.Count) return;
        if (tabs.Items[index] is not WpfTabItem tabItem) return;
        tabItem.Header = isDirty ? $"{baseHeader} *" : baseHeader;
    }

    private void UpdatePresetRecommendation(string currentPreset)
    {
        if (PresetSchemeRecommendationText == null)
        {
            return;
        }

        var recommendedScheme = _presetRecommendation.Scheme;
        var isRecommendedValid = PresetSchemePolicy.TryResolveManagedParameters(recommendedScheme, out _);
        if (!isRecommendedValid)
        {
            PresetSchemeRecommendationText.Text = string.Empty;
            PresetSchemeRecommendationText.Visibility = Visibility.Collapsed;
            return;
        }

        var recommendedLabel = ResolvePresetDisplayName(recommendedScheme);
        bool alreadyApplied = string.Equals(currentPreset, recommendedScheme, StringComparison.OrdinalIgnoreCase);
        var prefix = alreadyApplied
            ? $"设备画像推荐：{recommendedLabel}（已应用）。"
            : $"设备画像推荐：{recommendedLabel}。";
        var recommendation = $"{prefix}{_presetRecommendation.Reason}";
        PresetSchemeRecommendationText.Text = recommendation;
        PresetSchemeRecommendationText.Visibility = string.IsNullOrWhiteSpace(recommendation)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    private static string ResolvePresetDisplayName(string scheme)
    {
        foreach (var (label, value) in PresetSchemeChoices)
        {
            if (string.Equals(value, scheme, StringComparison.OrdinalIgnoreCase))
            {
                return label;
            }
        }

        return "课堂平衡（推荐）";
    }

    private void OnSceneCardsGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplySceneCardsLayout(e.NewSize.Width);
    }

    private void OnAdvancedModeCheckChanged(object sender, RoutedEventArgs e)
    {
        _advancedModeEnabled = AdvancedModeCheck?.IsChecked == true;
        UpdateAdvancedModeVisibility();
    }

    private void UpdateAdvancedModeVisibility()
    {
        var visibility = _advancedModeEnabled ? Visibility.Visible : Visibility.Collapsed;
        if (PhotoAdvancedExpander != null)
        {
            PhotoAdvancedExpander.Visibility = visibility;
            if (!_advancedModeEnabled)
            {
                PhotoAdvancedExpander.IsExpanded = false;
            }
        }

        if (PresentationAdvancedExpander != null)
        {
            PresentationAdvancedExpander.Visibility = visibility;
            if (!_advancedModeEnabled)
            {
                PresentationAdvancedExpander.IsExpanded = false;
            }
        }
    }

    private void ApplySceneCardsLayout(double availableWidth)
    {
        if (PhotoPdfSettingsCard == null || WpsSettingsCard == null)
        {
            return;
        }

        var layoutMode = SceneCardsLayoutPolicy.Resolve(availableWidth);
        if (layoutMode == SceneCardsLayoutMode.SingleColumn)
        {
            WpfGrid.SetRow(PhotoPdfSettingsCard, 0);
            WpfGrid.SetColumn(PhotoPdfSettingsCard, 0);
            WpfGrid.SetColumnSpan(PhotoPdfSettingsCard, 2);
            PhotoPdfSettingsCard.Margin = new Thickness(0, 0, 0, 8);

            WpfGrid.SetRow(WpsSettingsCard, 1);
            WpfGrid.SetColumn(WpsSettingsCard, 0);
            WpfGrid.SetColumnSpan(WpsSettingsCard, 2);
            WpsSettingsCard.Margin = new Thickness(0, 8, 0, 0);
            return;
        }

        WpfGrid.SetRow(PhotoPdfSettingsCard, 0);
        WpfGrid.SetColumn(PhotoPdfSettingsCard, 0);
        WpfGrid.SetColumnSpan(PhotoPdfSettingsCard, 1);
        PhotoPdfSettingsCard.Margin = new Thickness(0, 0, 6, 0);

        WpfGrid.SetRow(WpsSettingsCard, 0);
        WpfGrid.SetColumn(WpsSettingsCard, 1);
        WpfGrid.SetColumnSpan(WpsSettingsCard, 1);
        WpsSettingsCard.Margin = new Thickness(6, 0, 0, 0);
    }


    private void UpdateBrushSizeLabel()
    {
        if (BrushSizeValue == null)
        {
            return;
        }
        BrushSizeValue.Text = $"{Math.Round(BrushSizeSlider.Value)}px";
    }

    private void UpdateBrushOpacityLabel()
    {
        if (BrushOpacityValue == null)
        {
            return;
        }
        BrushOpacityValue.Text = $"{Math.Round(BrushOpacitySlider.Value)}%";
    }

    private void UpdateEraserSizeLabel()
    {
        if (EraserSizeValue == null)
        {
            return;
        }
        EraserSizeValue.Text = $"{Math.Round(EraserSizeSlider.Value)}px";
    }

    private void UpdateCalligraphyOverlayThresholdLabel()
    {
        if (CalligraphyOverlayThresholdValue == null)
        {
            return;
        }
        CalligraphyOverlayThresholdValue.Text = $"{Math.Round(CalligraphyOverlayThresholdSlider.Value)}%";
    }

    private void UpdateCalligraphyOptionState()
    {
        bool isCalligraphy = ResolveBrushStyle() == PaintBrushStyle.Calligraphy;
        CalligraphyPresetCombo.Visibility = isCalligraphy ? Visibility.Visible : Visibility.Collapsed;
        WhiteboardPresetCombo.Visibility = isCalligraphy ? Visibility.Collapsed : Visibility.Visible;
        CalligraphyPresetCombo.IsEnabled = isCalligraphy;
        WhiteboardPresetCombo.IsEnabled = !isCalligraphy;
        if (CalligraphyAdvancedExpander != null)
        {
            CalligraphyAdvancedExpander.Visibility = isCalligraphy ? Visibility.Visible : Visibility.Collapsed;
            if (!isCalligraphy)
            {
                CalligraphyAdvancedExpander.IsExpanded = false;
            }
        }
        CalligraphyInkBloomCheck.IsEnabled = isCalligraphy;
        CalligraphySealCheck.IsEnabled = isCalligraphy;
        CalligraphyOverlayThresholdLabel.IsEnabled = isCalligraphy;
        CalligraphyOverlayThresholdSlider.IsEnabled = isCalligraphy;
        CalligraphyOverlayThresholdValue.IsEnabled = isCalligraphy;
    }

    private void UpdateClassroomWritingModeHint(ClassroomWritingMode mode)
    {
        if (ClassroomWritingModeHint == null)
        {
            return;
        }
        if (!ClassroomWritingModeHints.TryGetValue(mode, out var hint))
        {
            hint = ClassroomWritingModeHints[ClassroomWritingMode.Balanced];
        }
        ClassroomWritingModeHint.Text = hint;
    }
    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            _ = this.SafeDragMove();
        }
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Max(min, Math.Min(max, value));
    }

    private static int ToPercent(byte value)
    {
        return (int)Math.Round(value * PaintSettingsDefaults.PercentMax / PaintSettingsDefaults.PercentToByteScale);
    }

    private static byte ToByte(double percent)
    {
        var clamped = Math.Max(PaintSettingsDefaults.PercentMin, Math.Min(PaintSettingsDefaults.PercentMax, percent));
        return (byte)Math.Clamp(
            (int)Math.Round(clamped * PaintSettingsDefaults.PercentToByteScale / PaintSettingsDefaults.PercentMax),
            0,
            255);
    }

    private void SelectShapeType(PaintShapeType type)
    {
        foreach (var item in ShapeCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is PaintShapeType tagged && tagged == type)
            {
                ShapeCombo.SelectedItem = item;
                return;
            }
        }
        ShapeCombo.SelectedIndex = 0;
    }

    private PaintShapeType ResolveShapeType()
    {
        if (ShapeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is PaintShapeType type)
        {
            return type;
        }
        return PaintShapeType.None;
    }

    private static string GetSelectedTag(WpfComboBox combo, string fallback)
    {
        if (combo.SelectedItem is WpfComboBoxItem item && item.Tag is string text)
        {
            return text;
        }
        return fallback;
    }

    private static void SelectComboByTag(WpfComboBox combo, string value, string fallback)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if ((item.Tag as string ?? string.Empty) == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if ((item.Tag as string ?? string.Empty) == fallback)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static void SelectComboByTag(WpfComboBox combo, double value)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tag && Math.Abs(tag - value) < PaintSettingsDefaults.ComboTagComparisonEpsilon)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private void SelectBrushStyle(PaintBrushStyle style)
    {
        foreach (var item in BrushStyleCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is PaintBrushStyle tagged && tagged == style)
            {
                BrushStyleCombo.SelectedItem = item;
                return;
            }
        }
        BrushStyleCombo.SelectedIndex = 0;
    }

    private PaintBrushStyle ResolveBrushStyle()
    {
        if (BrushStyleCombo.SelectedItem is WpfComboBoxItem item && item.Tag is PaintBrushStyle style)
        {
            return style;
        }
        return PaintBrushStyle.StandardRibbon;
    }

    private void SelectWhiteboardPreset(WhiteboardBrushPreset preset)
    {
        foreach (var item in WhiteboardPresetCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is WhiteboardBrushPreset tagged && tagged == preset)
            {
                WhiteboardPresetCombo.SelectedItem = item;
                return;
            }
        }
        WhiteboardPresetCombo.SelectedIndex = 0;
    }

    private void SelectCalligraphyPreset(CalligraphyBrushPreset preset)
    {
        foreach (var item in CalligraphyPresetCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is CalligraphyBrushPreset tagged && tagged == preset)
            {
                CalligraphyPresetCombo.SelectedItem = item;
                return;
            }
        }
        CalligraphyPresetCombo.SelectedIndex = 0;
    }

    private WhiteboardBrushPreset ResolveWhiteboardPreset()
    {
        if (WhiteboardPresetCombo.SelectedItem is WpfComboBoxItem item && item.Tag is WhiteboardBrushPreset preset)
        {
            return preset;
        }
        return WhiteboardBrushPreset.Smooth;
    }

    private CalligraphyBrushPreset ResolveCalligraphyPreset()
    {
        if (CalligraphyPresetCombo.SelectedItem is WpfComboBoxItem item && item.Tag is CalligraphyBrushPreset preset)
        {
            return preset;
        }
        return CalligraphyBrushPreset.Sharp;
    }

    private void SelectClassroomWritingMode(ClassroomWritingMode mode)
    {
        foreach (var item in ClassroomWritingModeCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is ClassroomWritingMode tagged && tagged == mode)
            {
                ClassroomWritingModeCombo.SelectedItem = item;
                return;
            }
        }
        ClassroomWritingModeCombo.SelectedIndex = 1;
    }

    private ClassroomWritingMode ResolveClassroomWritingMode()
    {
        if (ClassroomWritingModeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is ClassroomWritingMode mode)
        {
            return mode;
        }
        return ClassroomWritingMode.Balanced;
    }

    private static double FindNearestScale(double value)
    {
        var target = Clamp(value, ToolbarScaleDefaults.Min, ToolbarScaleDefaults.Max);
        return ToolbarScaleChoices.OrderBy(choice => Math.Abs(choice - target)).First();
    }

    private double GetSelectedScale()
    {
        if (ToolbarScaleCombo.SelectedItem is WpfComboBoxItem item && item.Tag is double scale)
        {
            return scale;
        }
        return 1.0;
    }

    private void SelectInkExportScope(InkExportScope scope)
    {
        foreach (var item in InkExportScopeCombo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is InkExportScope tagged && tagged == scope)
            {
                InkExportScopeCombo.SelectedItem = item;
                return;
            }
        }
        InkExportScopeCombo.SelectedIndex = 0;
    }

    private InkExportScope ResolveInkExportScope()
    {
        if (InkExportScopeCombo.SelectedItem is WpfComboBoxItem item && item.Tag is InkExportScope scope)
        {
            return scope;
        }
        return InkExportScope.AllPersistedAndSession;
    }

    private static void SelectIntCombo(WpfComboBox combo, int value, int fallback)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is int tagged && tagged == value)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is int tagged && tagged == fallback)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static void EnsureIntComboOption(WpfComboBox combo, int value, string label)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is int tagged && tagged == value)
            {
                return;
            }
        }

        combo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
    }

    private static int ResolveIntCombo(WpfComboBox combo, int fallback)
    {
        if (combo.SelectedItem is WpfComboBoxItem item && item.Tag is int value)
        {
            return value;
        }
        return fallback;
    }

    private static void SelectDoubleCombo(WpfComboBox combo, double value, double fallback)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tagged && Math.Abs(tagged - value) < PaintSettingsDefaults.DoubleComparisonEpsilon)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tagged && Math.Abs(tagged - fallback) < PaintSettingsDefaults.DoubleComparisonEpsilon)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private static void EnsureDoubleComboOption(WpfComboBox combo, double value, string label)
    {
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tagged
                && Math.Abs(tagged - value) < PaintSettingsDefaults.DoubleComparisonEpsilon)
            {
                return;
            }
        }

        combo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
    }

    private static double ResolveDoubleCombo(WpfComboBox combo, double fallback)
    {
        if (combo.SelectedItem is WpfComboBoxItem item && item.Tag is double value)
        {
            return value;
        }
        return fallback;
    }
}
