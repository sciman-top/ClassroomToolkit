using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;
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
        ("矩形（实心）", PaintShapeType.RectangleFill),
        ("圆形", PaintShapeType.Ellipse)
    };
    private static readonly (string Label, string Value)[] WpsModeChoices =
    {
        ("自动判断（推荐）", "auto"),
        ("强制原始输入（SendInput）", "raw"),
        ("强制消息投递（PostMessage）", "message")
    };
    private static readonly double[] ToolbarScaleChoices = { 0.8, 1.0, 1.25, 1.5, 1.75, 2.0 };

    public bool ControlMsPpt { get; private set; }
    public bool ControlWpsPpt { get; private set; }
    public string WpsInputMode { get; private set; } = "auto";
    public bool WpsWheelForward { get; private set; }
    public double BrushSize { get; private set; }
    public byte BrushOpacity { get; private set; }
    public double EraserSize { get; private set; }
    public byte BoardOpacity { get; private set; }
    public PaintShapeType ShapeType { get; private set; } = PaintShapeType.Line;
    public MediaColor BrushColor { get; private set; }
    public double ToolbarScale { get; private set; } = 1.0;

    public PaintSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        BrushColor = settings.BrushColor;
        foreach (var (label, value) in WpsModeChoices)
        {
            WpsModeCombo.Items.Add(new WpfComboBoxItem { Content = label, Tag = value });
        }
        SelectComboByTag(WpsModeCombo, settings.WpsInputMode, "auto");
        WpsWheelCheck.IsChecked = settings.WpsWheelForward;

        BrushSizeSlider.Value = Clamp(settings.BrushSize, 1, 50);
        EraserSizeSlider.Value = Clamp(settings.EraserSize, 6, 60);
        BrushOpacitySlider.Value = ToPercent(settings.BrushOpacity);
        BoardOpacitySlider.Value = ToPercent(settings.BoardOpacity);

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
        SelectComboByTag(ToolbarScaleCombo, selectedScale, 1.0);

        UpdateBrushSizeLabel();
        UpdateBrushOpacityLabel();
        UpdateEraserSizeLabel();
        UpdateBoardOpacityLabel();
        HighlightTempColorByValue(BrushColor);
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        ControlMsPpt = true;
        ControlWpsPpt = true;
        WpsInputMode = GetSelectedTag(WpsModeCombo, "auto");
        WpsWheelForward = WpsWheelCheck.IsChecked == true;
        BrushSize = Clamp(BrushSizeSlider.Value, 1, 50);
        EraserSize = Clamp(EraserSizeSlider.Value, 6, 60);
        BrushOpacity = ToByte(BrushOpacitySlider.Value);
        BoardOpacity = ToByte(BoardOpacitySlider.Value);
        ShapeType = ResolveShapeType();
        ToolbarScale = GetSelectedScale();
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

    private void OnEraserSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateEraserSizeLabel();
    }

    private void OnBoardOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateBoardOpacityLabel();
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

    private void UpdateBoardOpacityLabel()
    {
        if (BoardOpacityValue == null)
        {
            return;
        }
        BoardOpacityValue.Text = $"{Math.Round(BoardOpacitySlider.Value)}%";
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
            child.BorderBrush = MediaBrushes.Transparent;
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
        foreach (var item in ShapeCombo.Items.OfType<ComboBoxItem>())
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
}
