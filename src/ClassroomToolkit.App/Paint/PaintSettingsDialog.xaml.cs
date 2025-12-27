using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;

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

    public bool ControlMsPpt { get; private set; }
    public bool ControlWpsPpt { get; private set; }
    public string WpsInputMode { get; private set; } = "auto";
    public bool WpsWheelForward { get; private set; }
    public double BrushSize { get; private set; }
    public byte BrushOpacity { get; private set; }
    public double EraserSize { get; private set; }
    public byte BoardOpacity { get; private set; }
    public PaintShapeType ShapeType { get; private set; } = PaintShapeType.Line;

    public PaintSettingsDialog(AppSettings settings)
    {
        InitializeComponent();
        ControlOfficeCheck.IsChecked = settings.ControlMsPpt;
        ControlWpsCheck.IsChecked = settings.ControlWpsPpt;
        WpsModeCombo.ItemsSource = new[] { "auto", "raw", "message" };
        WpsModeCombo.SelectedItem = settings.WpsInputMode;
        if (WpsModeCombo.SelectedItem == null)
        {
            WpsModeCombo.SelectedIndex = 0;
        }
        WpsWheelCheck.IsChecked = settings.WpsWheelForward;

        BrushSizeSlider.Value = Clamp(settings.BrushSize, 1, 50);
        EraserSizeSlider.Value = Clamp(settings.EraserSize, 6, 60);
        BrushOpacitySlider.Value = ToPercent(settings.BrushOpacity);
        BoardOpacitySlider.Value = ToPercent(settings.BoardOpacity);

        foreach (var (label, type) in ShapeChoices)
        {
            var item = new ComboBoxItem { Content = label, Tag = type };
            ShapeCombo.Items.Add(item);
        }
        SelectShapeType(settings.ShapeType);

        UpdateBrushSizeLabel();
        UpdateBrushOpacityLabel();
        UpdateEraserSizeLabel();
        UpdateBoardOpacityLabel();
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnConfirm(object sender, RoutedEventArgs e)
    {
        ControlMsPpt = ControlOfficeCheck.IsChecked == true;
        ControlWpsPpt = ControlWpsCheck.IsChecked == true;
        WpsInputMode = WpsModeCombo.SelectedItem as string ?? "auto";
        WpsWheelForward = WpsWheelCheck.IsChecked == true;
        BrushSize = Clamp(BrushSizeSlider.Value, 1, 50);
        EraserSize = Clamp(EraserSizeSlider.Value, 6, 60);
        BrushOpacity = ToByte(BrushOpacitySlider.Value);
        BoardOpacity = ToByte(BoardOpacitySlider.Value);
        ShapeType = ResolveShapeType();
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
        if (ShapeCombo.SelectedItem is ComboBoxItem item && item.Tag is PaintShapeType type)
        {
            return type;
        }
        return PaintShapeType.None;
    }
}
