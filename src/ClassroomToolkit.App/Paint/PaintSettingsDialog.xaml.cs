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


}
