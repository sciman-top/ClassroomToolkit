using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Settings;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;

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
        ("自动判断（推荐）", "auto"),
        ("强制原始输入（SendInput）", "raw"),
        ("强制消息投递（PostMessage）", "message")
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
    private static readonly double[] ToolbarScaleChoices = { 0.8, 1.0, 1.25, 1.5, 1.75, 2.0 };
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
        ("更快（80ms）", 80),
        ("平衡（120ms，推荐/默认）", 120),
        ("稳定（140ms）", 140),
        ("跨屏稳（160ms）", 160),
        ("保守（180ms）", 180)
    };
    private static readonly (string Label, double Value)[] WheelZoomBaseChoices =
    {
        ("细腻（慢）", 1.0006),
        ("跨屏稳（中慢）", 1.0007),
        ("平衡（推荐/默认）", 1.0008),
        ("迅速（快）", 1.0010)
    };
    private static readonly (string Label, double Value)[] GestureSensitivityChoices =
    {
        ("柔和（0.8x）", 0.8),
        ("跨屏稳（0.9x）", 0.9),
        ("标准（1.0x，推荐/默认）", 1.0),
        ("灵敏（1.2x）", 1.2)
    };
    private static readonly (string Label, string Value)[] PresetSchemeChoices =
    {
        ("自定义（不覆盖）", "custom"),
        ("课堂平衡（推荐）", "balanced"),
        ("高灵敏（流畅优先）", "responsive"),
        ("高稳定（容错优先）", "stable"),
        ("双屏投影（跨屏优先）", "dual_screen")
    };
    private static readonly Dictionary<string, string> PresetHints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["custom"] = "保持你当前设置，不自动覆盖参数。",
        ["balanced"] = "课堂通用推荐：联动“课堂通用”书写模式，整体稳和快均衡。",
        ["responsive"] = "流畅优先：联动“跟手优先”书写模式，适合高性能设备。",
        ["stable"] = "稳定优先：联动“稳笔模式”，适合老旧设备或复杂环境。",
        ["dual_screen"] = "跨屏优先：联动“稳笔模式”，适合主屏+投影授课。"
    };

    public bool ControlMsPpt { get; private set; }
    public bool ControlWpsPpt { get; private set; }
    public string WpsInputMode { get; private set; } = "auto";
    public bool WpsWheelForward { get; private set; }
    public int WpsDebounceMs { get; private set; } = 200;
    public bool PresentationLockStrategyWhenDegraded { get; private set; } = true;
    public bool ForcePresentationForegroundOnFullscreen { get; private set; }
    public double BrushSize { get; private set; }
    public byte BrushOpacity { get; private set; }
    public PaintBrushStyle BrushStyle { get; private set; } = PaintBrushStyle.StandardRibbon;
    public WhiteboardBrushPreset WhiteboardPreset { get; private set; } = WhiteboardBrushPreset.Smooth;
    public CalligraphyBrushPreset CalligraphyPreset { get; private set; } = CalligraphyBrushPreset.Sharp;
    public string PresetScheme { get; private set; } = "custom";
    public ClassroomWritingMode ClassroomWritingMode { get; private set; } = ClassroomWritingMode.Balanced;
    public bool CalligraphyInkBloomEnabled { get; private set; }
    public bool CalligraphySealEnabled { get; private set; }
    public byte CalligraphyOverlayOpacityThreshold { get; private set; }
    public double EraserSize { get; private set; }
    public PaintShapeType ShapeType { get; private set; } = PaintShapeType.Line;
    public MediaColor BrushColor { get; private set; }
    public double ToolbarScale { get; private set; } = 1.0;
    public bool InkSaveEnabled { get; private set; }
    public InkExportScope InkExportScope { get; private set; } = InkExportScope.AllPersistedAndSession;
    public int InkExportMaxParallelFiles { get; private set; } = 2;
    public bool PhotoRememberTransform { get; private set; }
    public bool PhotoCrossPageDisplay { get; private set; }
    public bool PhotoInputTelemetryEnabled { get; private set; }
    public int PhotoNeighborPrefetchRadiusMax { get; private set; } = 4;
    public int PhotoPostInputRefreshDelayMs { get; private set; } = 120;
    public double PhotoWheelZoomBase { get; private set; } = 1.0008;
    public double PhotoGestureZoomSensitivity { get; private set; } = 1.0;
    private bool _suppressPresetSelectionChanged;
    private bool _sizeToContentCommitted;

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
        SelectComboByTag(WpsModeCombo, settings.WpsInputMode, "auto");
        _suppressPresetSelectionChanged = true;
        foreach (var (label, value) in PresetSchemeChoices)
        {
            PresetSchemeCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        var initialPreset = ResolveInitialPresetScheme(settings);
        SelectComboByTag(PresetSchemeCombo, initialPreset, "custom");
        UpdatePresetHint(initialPreset);
        _suppressPresetSelectionChanged = false;
        foreach (var (label, value) in WpsDebounceChoices)
        {
            WpsDebounceCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectIntCombo(WpsDebounceCombo, settings.WpsDebounceMs, fallback: 200);
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
        SelectIntCombo(ExportParallelCombo, settings.InkExportMaxParallelFiles, fallback: 2);
        foreach (var (label, value) in NeighborPrefetchChoices)
        {
            NeighborPrefetchCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectIntCombo(NeighborPrefetchCombo, settings.PhotoNeighborPrefetchRadiusMax, fallback: 4);
        foreach (var (label, value) in PostInputRefreshDelayChoices)
        {
            PostInputRefreshDelayCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectIntCombo(PostInputRefreshDelayCombo, settings.PhotoPostInputRefreshDelayMs, fallback: 120);
        foreach (var (label, value) in WheelZoomBaseChoices)
        {
            WheelZoomBaseCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectDoubleCombo(WheelZoomBaseCombo, settings.PhotoWheelZoomBase, fallback: 1.0008);
        foreach (var (label, value) in GestureSensitivityChoices)
        {
            GestureSensitivityCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectDoubleCombo(GestureSensitivityCombo, settings.PhotoGestureZoomSensitivity, fallback: 1.0);
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
        HighlightTempColorByValue(BrushColor);
        Loaded += (_, _) =>
        {
            WindowPlacementHelper.EnsureVisible(this);
            if (_sizeToContentCommitted)
            {
                return;
            }

            _sizeToContentCommitted = true;
            Dispatcher.BeginInvoke(new Action(() => SizeToContent = System.Windows.SizeToContent.Manual),
                System.Windows.Threading.DispatcherPriority.ContextIdle);
        };
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        ControlMsPpt = true;
        ControlWpsPpt = true;
        WpsInputMode = GetSelectedTag(WpsModeCombo, "auto");
        PresetScheme = GetSelectedTag(PresetSchemeCombo, "custom");
        WpsWheelForward = WpsWheelCheck.IsChecked == true;
        WpsDebounceMs = ResolveIntCombo(WpsDebounceCombo, fallback: 200);
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
        InkExportMaxParallelFiles = ResolveIntCombo(ExportParallelCombo, fallback: 2);
        PhotoRememberTransform = PhotoRememberTransformCheck.IsChecked == true;
        PhotoCrossPageDisplay = PhotoCrossPageDisplayCheck.IsChecked == true;
        PhotoInputTelemetryEnabled = PhotoInputTelemetryCheck.IsChecked == true;
        PhotoNeighborPrefetchRadiusMax = ResolveIntCombo(NeighborPrefetchCombo, fallback: 4);
        PhotoPostInputRefreshDelayMs = ResolveIntCombo(PostInputRefreshDelayCombo, fallback: 120);
        PhotoWheelZoomBase = ResolveDoubleCombo(WheelZoomBaseCombo, fallback: 1.0008);
        PhotoGestureZoomSensitivity = ResolveDoubleCombo(GestureSensitivityCombo, fallback: 1.0);
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
        var preset = GetSelectedTag(PresetSchemeCombo, "custom");
        UpdatePresetHint(preset);
        ApplyPresetScheme(preset);
    }

    private void ApplyPresetScheme(string preset)
    {
        if (string.Equals(preset, "custom", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        SelectComboByTag(WpsModeCombo, "auto", "auto");
        LockStrategyOnDegradeCheck.IsChecked = true;
        WpsWheelCheck.IsChecked = true;

        switch (preset)
        {
            case "balanced":
                SelectClassroomWritingMode(ClassroomWritingMode.Balanced);
                SelectIntCombo(WpsDebounceCombo, 120, fallback: 120);
                SelectIntCombo(PostInputRefreshDelayCombo, 120, fallback: 120);
                SelectDoubleCombo(WheelZoomBaseCombo, 1.0008, fallback: 1.0008);
                SelectDoubleCombo(GestureSensitivityCombo, 1.0, fallback: 1.0);
                break;
            case "responsive":
                SelectClassroomWritingMode(ClassroomWritingMode.Responsive);
                SelectIntCombo(WpsDebounceCombo, 80, fallback: 80);
                SelectIntCombo(PostInputRefreshDelayCombo, 80, fallback: 80);
                SelectDoubleCombo(WheelZoomBaseCombo, 1.0010, fallback: 1.0010);
                SelectDoubleCombo(GestureSensitivityCombo, 1.2, fallback: 1.2);
                break;
            case "stable":
                SelectClassroomWritingMode(ClassroomWritingMode.Stable);
                SelectIntCombo(WpsDebounceCombo, 200, fallback: 200);
                SelectIntCombo(PostInputRefreshDelayCombo, 140, fallback: 140);
                SelectDoubleCombo(WheelZoomBaseCombo, 1.0006, fallback: 1.0006);
                SelectDoubleCombo(GestureSensitivityCombo, 0.8, fallback: 0.8);
                break;
            case "dual_screen":
                SelectClassroomWritingMode(ClassroomWritingMode.Stable);
                SelectIntCombo(WpsDebounceCombo, 160, fallback: 160);
                SelectIntCombo(PostInputRefreshDelayCombo, 160, fallback: 160);
                SelectDoubleCombo(WheelZoomBaseCombo, 1.0007, fallback: 1.0007);
                SelectDoubleCombo(GestureSensitivityCombo, 0.9, fallback: 0.9);
                break;
        }

        UpdateClassroomWritingModeHint(ResolveClassroomWritingMode());
    }

    private static string ResolveInitialPresetScheme(AppSettings settings)
    {
        var configured = (settings.PresetScheme ?? string.Empty).Trim().ToLowerInvariant();
        if (configured is "custom" or "balanced" or "responsive" or "stable" or "dual_screen")
        {
            return configured;
        }

        if (settings.ClassroomWritingMode == ClassroomWritingMode.Balanced &&
            settings.WpsDebounceMs == 120 &&
            settings.PhotoPostInputRefreshDelayMs == 120 &&
            Math.Abs(settings.PhotoWheelZoomBase - 1.0008) < 0.0001 &&
            Math.Abs(settings.PhotoGestureZoomSensitivity - 1.0) < 0.0001)
        {
            return "balanced";
        }

        if (settings.ClassroomWritingMode == ClassroomWritingMode.Responsive &&
            settings.WpsDebounceMs == 80 &&
            settings.PhotoPostInputRefreshDelayMs == 80 &&
            Math.Abs(settings.PhotoWheelZoomBase - 1.0010) < 0.0001 &&
            Math.Abs(settings.PhotoGestureZoomSensitivity - 1.2) < 0.0001)
        {
            return "responsive";
        }

        if (settings.ClassroomWritingMode == ClassroomWritingMode.Stable &&
            settings.WpsDebounceMs == 200 &&
            settings.PhotoPostInputRefreshDelayMs == 140 &&
            Math.Abs(settings.PhotoWheelZoomBase - 1.0006) < 0.0001 &&
            Math.Abs(settings.PhotoGestureZoomSensitivity - 0.8) < 0.0001)
        {
            return "stable";
        }

        if (settings.ClassroomWritingMode == ClassroomWritingMode.Stable &&
            settings.WpsDebounceMs == 160 &&
            settings.PhotoPostInputRefreshDelayMs == 160 &&
            Math.Abs(settings.PhotoWheelZoomBase - 1.0007) < 0.0001 &&
            Math.Abs(settings.PhotoGestureZoomSensitivity - 0.9) < 0.0001)
        {
            return "dual_screen";
        }

        return "custom";
    }

    private void UpdatePresetHint(string preset)
    {
        if (PresetSchemeHintText == null)
        {
            return;
        }
        if (!PresetHints.TryGetValue(preset, out var hint))
        {
            hint = PresetHints["custom"];
        }
        PresetSchemeHintText.Text = hint;
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



    private void OnTempColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not WpfButton button)
        {
            return;
        }
        var hex = button.Tag as string;
        if (string.IsNullOrWhiteSpace(hex))
        {
            return;
        }
        BrushColor = (MediaColor)MediaColorConverter.ConvertFromString(hex);
        HighlightTempColor(button);
    }

    private void HighlightTempColor(WpfButton selected)
    {
        if (selected == null)
        {
            return;
        }
        var parent = VisualTreeHelper.GetParent(selected) as System.Windows.Controls.Panel;
        if (parent == null)
        {
            return;
        }
        foreach (var child in parent.Children.OfType<WpfButton>())
        {
            child.BorderThickness = new Thickness(1);
            child.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(0x20, 0, 0, 0));
        }
        selected.BorderThickness = new Thickness(2);
        selected.BorderBrush = MediaBrushes.DeepSkyBlue;
    }

    private void HighlightTempColorByValue(MediaColor color)
    {
        var hex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        foreach (var button in FindTempColorButtons())
        {
            var tag = button.Tag as string;
            if (string.Equals(tag, hex, StringComparison.OrdinalIgnoreCase))
            {
                HighlightTempColor(button);
                return;
            }
        }
    }

    private IEnumerable<WpfButton> FindTempColorButtons()
    {
        return FindVisualChildren<WpfButton>(this)
            .Where(btn => btn.Tag is string tag && tag.StartsWith("#", StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null)
        {
            yield break;
        }
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match)
            {
                yield return match;
            }
            foreach (var nested in FindVisualChildren<T>(child))
            {
                yield return nested;
            }
        }
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
        return (int)Math.Round(value * 100.0 / 255.0);
    }

    private static byte ToByte(double percent)
    {
        var clamped = Math.Max(0, Math.Min(100, percent));
        return (byte)Math.Clamp((int)Math.Round(clamped * 255.0 / 100.0), 0, 255);
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
            if (item.Tag is double tag && Math.Abs(tag - value) < 0.001)
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
        var target = Clamp(value, 0.8, 2.0);
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
            if (item.Tag is double tagged && Math.Abs(tagged - value) < 0.0001)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        foreach (var item in combo.Items.OfType<WpfComboBoxItem>())
        {
            if (item.Tag is double tagged && Math.Abs(tagged - fallback) < 0.0001)
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
