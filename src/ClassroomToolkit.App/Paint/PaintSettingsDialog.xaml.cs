using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using MediaColor = System.Windows.Media.Color;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfGrid = System.Windows.Controls.Grid;

namespace ClassroomToolkit.App.Paint;

public partial class PaintSettingsDialog : Window
{
    private static readonly (string Label, PaintShapeType Type)[] ShapeChoices =
    {
        ("无", PaintShapeType.None),
        ("直线", PaintShapeType.Line),
        ("虚线", PaintShapeType.DashedLine),
        ("矩形", PaintShapeType.Rectangle),
        ("圆形", PaintShapeType.Ellipse)
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
    private static readonly (string Label, string Value)[] WpsModeChoices =
    {
        ("自动判断（推荐）", WpsInputModeDefaults.Auto),
        ("强制原始输入（SendInput）", WpsInputModeDefaults.Raw),
        ("强制消息投递（PostMessage）", WpsInputModeDefaults.Message)
    };
    private static readonly (string Label, int Value)[] WpsDebounceChoices =
    {
        ("关闭（0ms）", 0),
        ("80 ms（更灵敏）", 80),
        ("120 ms（推荐）", 120),
        ("160 ms（跨屏稳）", 160),
        ("200 ms（默认）", 200),
        ("300 ms（更稳）", 300)
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
        ("跨屏稳（160ms）", PaintPresetDefaults.PostInputDualScreenMs),
        ("保守（180ms）", 180)
    };
    private static readonly (string Label, double Value)[] WheelZoomBaseChoices =
    {
        ("细腻（慢）", PaintPresetDefaults.WheelZoomStable),
        ("跨屏稳（中慢）", PaintPresetDefaults.WheelZoomDualScreen),
        ("平衡（推荐/默认）", PaintPresetDefaults.WheelZoomBalanced),
        ("迅速（快）", PaintPresetDefaults.WheelZoomResponsive)
    };
    private static readonly (string Label, double Value)[] GestureSensitivityChoices =
    {
        ("柔和（0.8x）", PaintPresetDefaults.GestureSensitivityStable),
        ("跨屏稳（0.9x）", PaintPresetDefaults.GestureSensitivityDualScreen),
        ("标准（1.0x，推荐/默认）", PhotoZoomInputDefaults.GestureSensitivityDefault),
        ("灵敏（1.2x）", PaintPresetDefaults.GestureSensitivityResponsive)
    };
    private static readonly (string Label, string Value)[] PresetSchemeChoices =
    {
        ("自定义（不覆盖）", PresetSchemeDefaults.Custom),
        ("课堂平衡（推荐）", PresetSchemeDefaults.Balanced),
        ("高灵敏（流畅优先）", PresetSchemeDefaults.Responsive),
        ("高稳定（容错优先）", PresetSchemeDefaults.Stable),
        ("双屏投影（跨屏优先）", PresetSchemeDefaults.DualScreen)
    };
    private static readonly Dictionary<string, string> PresetHints = new(StringComparer.OrdinalIgnoreCase)
    {
        [PresetSchemeDefaults.Custom] = "保持你当前设置，不自动覆盖参数。手动修改联动参数后会自动切到此项。",
        [PresetSchemeDefaults.Balanced] = "课堂通用推荐：联动“课堂通用”书写模式，整体稳和快均衡。",
        [PresetSchemeDefaults.Responsive] = "流畅优先：联动“跟手优先”书写模式，适合高性能设备。",
        [PresetSchemeDefaults.Stable] = "稳定优先：联动“稳笔模式”，适合老旧设备或复杂环境。",
        [PresetSchemeDefaults.DualScreen] = "跨屏优先：联动“稳笔模式”，适合主屏+投影授课。"
    };
    private const string PresetManagedHintForCustom =
        "当前为自定义：可独立调整 WPS 模式、滚轮映射、降级锁定、去抖阈值、抬笔刷新、缩放步进与手势灵敏。";
    private const string PresetManagedHintForPreset =
        "当前预设托管：联动参数已锁定。点击“转为自定义后编辑”后可单独调整。";

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
        int WpsDebounceMs,
        int PhotoPostInputRefreshDelayMs,
        double PhotoWheelZoomBase,
        double PhotoGestureZoomSensitivity);

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
        string WpsInputMode,
        bool WpsWheelForward,
        bool ForcePresentationForegroundOnFullscreen,
        int WpsDebounceMs,
        bool LockStrategyWhenDegraded);

    private readonly record struct AdvancedSectionState(
        PaintShapeType ShapeType,
        double ToolbarScale);

    public bool ControlMsPpt { get; private set; }
    public bool ControlWpsPpt { get; private set; }
    public string WpsInputMode { get; private set; } = WpsInputModeDefaults.Auto;
    public bool WpsWheelForward { get; private set; }
    public int WpsDebounceMs { get; private set; } = PaintPresetDefaults.WpsDebounceDefaultMs;
    public bool PresentationLockStrategyWhenDegraded { get; private set; } = true;
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
    private bool _suppressPresetSelectionChanged;
    private bool _suppressPresetAutoCustom = true;
    private string _currentPresetScheme = PresetSchemeDefaults.Custom;
    private PresetSchemeManagedParameters _customManagedSnapshot;
    private bool _hasCustomManagedSnapshot;
    private bool _sizeToContentCommitted;
    private readonly PresetSchemeRecommendation _presetRecommendation;
    private bool _suppressSectionDirtyTracking = true;
    private PresetBrushSectionState _initialPresetBrushSectionState;
    private SceneSectionState _initialSceneSectionState;
    private AdvancedSectionState _initialAdvancedSectionState;

    public PaintSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        MaxHeight = Math.Max(360, SystemParameters.WorkArea.Height - 24);
        MaxWidth = Math.Max(560, SystemParameters.WorkArea.Width - 24);
        
        // 在构造函数中立即修复 BorderBrush 问题
        try
        {
            BorderFixHelper.FixAllBorders(this);
            System.Diagnostics.Debug.WriteLine("PaintSettingsDialog: 构造函数中修复完成");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"PaintSettingsDialog 构造函数修复失败: {ex.Message}");
        }
        
        BrushColor = settings.BrushColor;
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
        if (WritingModeOverrideExpander != null)
        {
            WritingModeOverrideExpander.IsExpanded = false;
        }
        _currentPresetScheme = initialPreset;
        _suppressPresetSelectionChanged = false;
        _presetRecommendation = PresetSchemePolicy.ResolveRecommendation(settings);
        foreach (var (label, value) in WpsDebounceChoices)
        {
            WpsDebounceCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectIntCombo(WpsDebounceCombo, settings.WpsDebounceMs, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);
        WpsWheelCheck.IsChecked = settings.WpsWheelForward;
        LockStrategyOnDegradeCheck.IsChecked = settings.PresentationLockStrategyWhenDegraded;
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
        SelectIntCombo(PostInputRefreshDelayCombo, settings.PhotoPostInputRefreshDelayMs, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs);
        foreach (var (label, value) in WheelZoomBaseChoices)
        {
            WheelZoomBaseCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectDoubleCombo(WheelZoomBaseCombo, settings.PhotoWheelZoomBase, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault);
        foreach (var (label, value) in GestureSensitivityChoices)
        {
            GestureSensitivityCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectDoubleCombo(GestureSensitivityCombo, settings.PhotoGestureZoomSensitivity, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault);
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
        Loaded += (_, _) =>
        {
            WindowPlacementHelper.EnsureVisible(this);
            if (_sizeToContentCommitted)
            {
                return;
            }

            _sizeToContentCommitted = true;
            _ = Dispatcher.InvokeAsync(
                () => SizeToContent = System.Windows.SizeToContent.Manual,
                System.Windows.Threading.DispatcherPriority.ContextIdle);
        };

        InitializeCustomSnapshotIfNeeded();
        AttachPresetManagedControlHandlers();
        AttachSectionDirtyTrackingHandlers();
        _suppressPresetAutoCustom = false;
        UpdatePresetHint(_currentPresetScheme);
        ApplySceneCardsLayout(SceneCardsGrid?.ActualWidth ?? 0);
        _initialPresetBrushSectionState = CapturePresetBrushSectionStateFromControls();
        _initialSceneSectionState = CaptureSceneSectionStateFromControls();
        _initialAdvancedSectionState = CaptureAdvancedSectionStateFromControls();
        _suppressSectionDirtyTracking = false;
        UpdateSectionDirtyStates();
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        ControlMsPpt = true;
        ControlWpsPpt = true;
        WpsInputMode = GetSelectedTag(WpsModeCombo, WpsInputModeDefaults.Auto);
        PresetScheme = GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom);
        WpsWheelForward = WpsWheelCheck.IsChecked == true;
        WpsDebounceMs = ResolveIntCombo(WpsDebounceCombo, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);
        PresentationLockStrategyWhenDegraded = LockStrategyOnDegradeCheck.IsChecked != false;
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
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
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
        UpdateWritingModeOverrideExpanderState(preset);
        ApplyPresetScheme(preset);
        _currentPresetScheme = preset;
    }

    private void OnReapplyPresetClick(object sender, RoutedEventArgs e)
    {
        var preset = GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom);
        if (string.Equals(preset, PresetSchemeDefaults.Custom, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ApplyPresetScheme(preset);
        UpdatePresetHint(preset);
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

    private void OnApplyRecommendedPresetClick(object sender, RoutedEventArgs e)
    {
        if (!PresetSchemePolicy.TryResolveManagedParameters(_presetRecommendation.Scheme, out _))
        {
            return;
        }

        var currentPreset = GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom);
        if (string.Equals(currentPreset, _presetRecommendation.Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectComboByTag(PresetSchemeCombo, _presetRecommendation.Scheme, PresetSchemeDefaults.Balanced);
    }

    private void OnResetPresetBrushSectionClick(object sender, RoutedEventArgs e)
    {
        ApplyPresetBrushSectionState(_initialPresetBrushSectionState);
        UpdateSectionDirtyStates();
    }

    private void OnResetSceneSectionClick(object sender, RoutedEventArgs e)
    {
        ApplySceneSectionState(_initialSceneSectionState);
        UpdateSectionDirtyStates();
    }

    private void OnResetAdvancedSectionClick(object sender, RoutedEventArgs e)
    {
        ApplyAdvancedSectionState(_initialAdvancedSectionState);
        UpdateSectionDirtyStates();
    }

    private void OnRestoreCustomPresetClick(object sender, RoutedEventArgs e)
    {
        if (!_hasCustomManagedSnapshot)
        {
            return;
        }

        if (!IsCustomScheme(GetSelectedTag(PresetSchemeCombo, PresetSchemeDefaults.Custom)))
        {
            return;
        }

        _suppressPresetAutoCustom = true;
        try
        {
            ApplyManagedParametersToControls(_customManagedSnapshot);
        }
        finally
        {
            _suppressPresetAutoCustom = false;
        }

        _currentPresetScheme = PresetSchemeDefaults.Custom;
        UpdateClassroomWritingModeHint(_customManagedSnapshot.ClassroomWritingMode);
        UpdatePresetHint(PresetSchemeDefaults.Custom);
        UpdateWritingModeOverrideExpanderState(PresetSchemeDefaults.Custom);
        System.Diagnostics.Debug.WriteLine($"[PaintPreset] restored custom snapshot: {FormatManagedParameters(_customManagedSnapshot)}");
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
        if (PresetManagedHintText != null)
        {
            var isCustom = IsCustomScheme(preset);
            PresetManagedHintText.Text = isCustom
                ? PresetManagedHintForCustom
                : PresetManagedHintForPreset;
        }
        if (ReapplyPresetButton != null)
        {
            ReapplyPresetButton.IsEnabled = !IsCustomScheme(preset);
        }
        if (RestoreCustomPresetButton != null)
        {
            RestoreCustomPresetButton.IsEnabled = IsCustomScheme(preset) && _hasCustomManagedSnapshot;
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
        WpsDebounceCombo.SelectionChanged += OnPresetManagedComboChanged;
        PostInputRefreshDelayCombo.SelectionChanged += OnPresetManagedComboChanged;
        WheelZoomBaseCombo.SelectionChanged += OnPresetManagedComboChanged;
        GestureSensitivityCombo.SelectionChanged += OnPresetManagedComboChanged;
        WpsWheelCheck.Checked += OnPresetManagedToggleChanged;
        WpsWheelCheck.Unchecked += OnPresetManagedToggleChanged;
        LockStrategyOnDegradeCheck.Checked += OnPresetManagedToggleChanged;
        LockStrategyOnDegradeCheck.Unchecked += OnPresetManagedToggleChanged;
    }

    private void AttachSectionDirtyTrackingHandlers()
    {
        BrushStyleCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        WhiteboardPresetCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        CalligraphyPresetCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        PresetSchemeCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        ClassroomWritingModeCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        BrushSizeSlider.ValueChanged += (_, _) => UpdateSectionDirtyStates();
        BrushOpacitySlider.ValueChanged += (_, _) => UpdateSectionDirtyStates();
        EraserSizeSlider.ValueChanged += (_, _) => UpdateSectionDirtyStates();
        CalligraphyInkBloomCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        CalligraphyInkBloomCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();
        CalligraphySealCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        CalligraphySealCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();
        CalligraphyOverlayThresholdSlider.ValueChanged += (_, _) => UpdateSectionDirtyStates();

        InkSaveCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        InkSaveCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();
        InkExportScopeCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        ExportParallelCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        PhotoCrossPageDisplayCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        PhotoCrossPageDisplayCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();
        PhotoRememberTransformCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        PhotoRememberTransformCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();
        PhotoInputTelemetryCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        PhotoInputTelemetryCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();
        NeighborPrefetchCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        PostInputRefreshDelayCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        WheelZoomBaseCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        GestureSensitivityCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        WpsModeCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        WpsWheelCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        WpsWheelCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();
        ForceForegroundCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        ForceForegroundCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();
        WpsDebounceCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        LockStrategyOnDegradeCheck.Checked += (_, _) => UpdateSectionDirtyStates();
        LockStrategyOnDegradeCheck.Unchecked += (_, _) => UpdateSectionDirtyStates();

        ShapeCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
        ToolbarScaleCombo.SelectionChanged += (_, _) => UpdateSectionDirtyStates();
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
        UpdateWritingModeOverrideExpanderState(PresetSchemeDefaults.Custom);
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
        _hasCustomManagedSnapshot = true;
        System.Diagnostics.Debug.WriteLine($"[PaintPreset] save custom snapshot: {FormatManagedParameters(_customManagedSnapshot)}");
    }

    private PresetSchemeManagedParameters CaptureManagedParametersFromControls()
    {
        return new PresetSchemeManagedParameters(
            GetSelectedTag(WpsModeCombo, WpsInputModeDefaults.Auto),
            WpsWheelCheck.IsChecked == true,
            LockStrategyOnDegradeCheck.IsChecked != false,
            ResolveClassroomWritingMode(),
            ResolveIntCombo(WpsDebounceCombo, fallback: PaintPresetDefaults.WpsDebounceDefaultMs),
            ResolveIntCombo(PostInputRefreshDelayCombo, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs),
            ResolveDoubleCombo(WheelZoomBaseCombo, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault),
            ResolveDoubleCombo(GestureSensitivityCombo, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault));
    }

    private void ApplyManagedParametersToControls(PresetSchemeManagedParameters parameters)
    {
        SelectComboByTag(WpsModeCombo, parameters.WpsInputMode, WpsInputModeDefaults.Auto);
        LockStrategyOnDegradeCheck.IsChecked = parameters.LockStrategyWhenDegraded;
        WpsWheelCheck.IsChecked = parameters.WpsWheelForward;
        SelectClassroomWritingMode(parameters.ClassroomWritingMode);
        SelectIntCombo(WpsDebounceCombo, parameters.WpsDebounceMs, fallback: parameters.WpsDebounceMs);
        SelectIntCombo(PostInputRefreshDelayCombo, parameters.PhotoPostInputRefreshDelayMs, fallback: parameters.PhotoPostInputRefreshDelayMs);
        SelectDoubleCombo(WheelZoomBaseCombo, parameters.PhotoWheelZoomBase, fallback: parameters.PhotoWheelZoomBase);
        SelectDoubleCombo(
            GestureSensitivityCombo,
            parameters.PhotoGestureZoomSensitivity,
            fallback: parameters.PhotoGestureZoomSensitivity);
    }

    private static string FormatManagedParameters(PresetSchemeManagedParameters parameters)
    {
        return $"mode={parameters.WpsInputMode}; wheel={parameters.WpsWheelForward}; lock={parameters.LockStrategyWhenDegraded}; " +
               $"writing={parameters.ClassroomWritingMode}; debounce={parameters.WpsDebounceMs}; postInput={parameters.PhotoPostInputRefreshDelayMs}; " +
               $"wheelZoom={parameters.PhotoWheelZoomBase:0.####}; gesture={parameters.PhotoGestureZoomSensitivity:0.###}";
    }

    private void UpdateManagedControlVisualState(string preset)
    {
        var isCustom = IsCustomScheme(preset);
        var tip = isCustom
            ? "自定义模式：该参数可独立调整。"
            : "预设托管：切换到“自定义”后可独立调整。";

        WpsModeCombo.ToolTip = tip;
        WpsDebounceCombo.ToolTip = tip;
        WpsWheelCheck.ToolTip = tip;
        LockStrategyOnDegradeCheck.ToolTip = tip;
        PostInputRefreshDelayCombo.ToolTip = tip;
        WheelZoomBaseCombo.ToolTip = tip;
        GestureSensitivityCombo.ToolTip = tip;
        ClassroomWritingModeCombo.ToolTip = tip;
        WpsModeCombo.IsEnabled = isCustom;
        WpsDebounceCombo.IsEnabled = isCustom;
        WpsWheelCheck.IsEnabled = isCustom;
        LockStrategyOnDegradeCheck.IsEnabled = isCustom;
        PostInputRefreshDelayCombo.IsEnabled = isCustom;
        WheelZoomBaseCombo.IsEnabled = isCustom;
        GestureSensitivityCombo.IsEnabled = isCustom;
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
            WpsDebounceMs: ResolveIntCombo(WpsDebounceCombo, fallback: PaintPresetDefaults.WpsDebounceDefaultMs),
            PhotoPostInputRefreshDelayMs: ResolveIntCombo(PostInputRefreshDelayCombo, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs),
            PhotoWheelZoomBase: ResolveDoubleCombo(WheelZoomBaseCombo, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault),
            PhotoGestureZoomSensitivity: ResolveDoubleCombo(GestureSensitivityCombo, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault));
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
            WpsInputMode: GetSelectedTag(WpsModeCombo, WpsInputModeDefaults.Auto),
            WpsWheelForward: WpsWheelCheck.IsChecked == true,
            ForcePresentationForegroundOnFullscreen: ForceForegroundCheck.IsChecked == true,
            WpsDebounceMs: ResolveIntCombo(WpsDebounceCombo, fallback: PaintPresetDefaults.WpsDebounceDefaultMs),
            LockStrategyWhenDegraded: LockStrategyOnDegradeCheck.IsChecked != false);
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
            SelectIntCombo(WpsDebounceCombo, state.WpsDebounceMs, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);
            SelectIntCombo(PostInputRefreshDelayCombo, state.PhotoPostInputRefreshDelayMs, fallback: PaintPresetDefaults.PostInputRefreshDefaultMs);
            SelectDoubleCombo(WheelZoomBaseCombo, state.PhotoWheelZoomBase, fallback: PhotoZoomInputDefaults.WheelZoomBaseDefault);
            SelectDoubleCombo(GestureSensitivityCombo, state.PhotoGestureZoomSensitivity, fallback: PhotoZoomInputDefaults.GestureSensitivityDefault);
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
        UpdateWritingModeOverrideExpanderState(state.PresetScheme);
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
            SelectComboByTag(WpsModeCombo, state.WpsInputMode, WpsInputModeDefaults.Auto);
            WpsWheelCheck.IsChecked = state.WpsWheelForward;
            ForceForegroundCheck.IsChecked = state.ForcePresentationForegroundOnFullscreen;
            SelectIntCombo(WpsDebounceCombo, state.WpsDebounceMs, fallback: PaintPresetDefaults.WpsDebounceDefaultMs);
            LockStrategyOnDegradeCheck.IsChecked = state.LockStrategyWhenDegraded;
        }
        finally
        {
            _suppressPresetAutoCustom = false;
            _suppressSectionDirtyTracking = false;
        }

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

    private void UpdateSectionDirtyStates()
    {
        if (_suppressSectionDirtyTracking)
        {
            return;
        }

        bool presetDirty = IsPresetBrushSectionDirty();
        bool sceneDirty = IsSceneSectionDirty();
        bool advancedDirty = IsAdvancedSectionDirty();
        if (PresetBrushSectionStateText != null)
        {
            PresetBrushSectionStateText.Text = presetDirty ? "本页状态：已修改" : "本页状态：未修改";
        }
        if (SceneSectionStateText != null)
        {
            SceneSectionStateText.Text = sceneDirty ? "本页状态：已修改" : "本页状态：未修改";
        }
        if (AdvancedSectionStateText != null)
        {
            AdvancedSectionStateText.Text = advancedDirty ? "本页状态：已修改" : "本页状态：未修改";
        }
        if (ResetPresetBrushSectionButton != null)
        {
            ResetPresetBrushSectionButton.IsEnabled = presetDirty;
        }
        if (ResetSceneSectionButton != null)
        {
            ResetSceneSectionButton.IsEnabled = sceneDirty;
        }
        if (ResetAdvancedSectionButton != null)
        {
            ResetAdvancedSectionButton.IsEnabled = advancedDirty;
        }
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
            || current.WpsDebounceMs != initial.WpsDebounceMs
            || current.PhotoPostInputRefreshDelayMs != initial.PhotoPostInputRefreshDelayMs
            || Math.Abs(current.PhotoWheelZoomBase - initial.PhotoWheelZoomBase) > PaintSettingsDefaults.DoubleComparisonEpsilon
            || Math.Abs(current.PhotoGestureZoomSensitivity - initial.PhotoGestureZoomSensitivity) > PaintSettingsDefaults.DoubleComparisonEpsilon;
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
            || !string.Equals(current.WpsInputMode, initial.WpsInputMode, StringComparison.OrdinalIgnoreCase)
            || current.WpsWheelForward != initial.WpsWheelForward
            || current.ForcePresentationForegroundOnFullscreen != initial.ForcePresentationForegroundOnFullscreen
            || current.WpsDebounceMs != initial.WpsDebounceMs
            || current.LockStrategyWhenDegraded != initial.LockStrategyWhenDegraded;
    }

    private bool IsAdvancedSectionDirty()
    {
        var current = CaptureAdvancedSectionStateFromControls();
        var initial = _initialAdvancedSectionState;
        return current.ShapeType != initial.ShapeType
            || Math.Abs(current.ToolbarScale - initial.ToolbarScale) > PaintSettingsDefaults.ComboTagComparisonEpsilon;
    }

    private void UpdatePresetRecommendation(string currentPreset)
    {
        if (PresetSchemeRecommendationText == null || ApplyRecommendedPresetButton == null)
        {
            return;
        }

        var recommendedScheme = _presetRecommendation.Scheme;
        var isRecommendedValid = PresetSchemePolicy.TryResolveManagedParameters(recommendedScheme, out _);
        if (!isRecommendedValid)
        {
            PresetSchemeRecommendationText.Text = string.Empty;
            ApplyRecommendedPresetButton.IsEnabled = false;
            return;
        }

        var recommendedLabel = ResolvePresetDisplayName(recommendedScheme);
        bool alreadyApplied = string.Equals(currentPreset, recommendedScheme, StringComparison.OrdinalIgnoreCase);
        var prefix = alreadyApplied
            ? $"设备画像推荐：{recommendedLabel}（已应用）。"
            : $"设备画像推荐：{recommendedLabel}。";
        PresetSchemeRecommendationText.Text = $"{prefix}{_presetRecommendation.Reason}";
        ApplyRecommendedPresetButton.IsEnabled = !alreadyApplied;
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

    private void UpdateWritingModeOverrideExpanderState(string preset)
    {
        if (WritingModeOverrideExpander == null)
        {
            return;
        }

        WritingModeOverrideExpander.IsExpanded = IsCustomScheme(preset);
    }

    private void OnSceneCardsGridSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplySceneCardsLayout(e.NewSize.Width);
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
        CalligraphyPresetCombo.IsEnabled = isCalligraphy;
        WhiteboardPresetCombo.IsEnabled = !isCalligraphy;
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
            DragMove();
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
        if (type == PaintShapeType.RectangleFill)
        {
            type = PaintShapeType.Rectangle;
        }
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

    private static double ResolveDoubleCombo(WpfComboBox combo, double fallback)
    {
        if (combo.SelectedItem is WpfComboBoxItem item && item.Tag is double value)
        {
            return value;
        }
        return fallback;
    }
}
