using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MediaColor = System.Windows.Media.Color;
using System.Windows.Media;
using ClassroomToolkit.App.Commands;

namespace ClassroomToolkit.App.Paint;

public partial class PaintToolbarWindow : Window
{
    public event Action<PaintToolMode>? ModeChanged;
    public event Action<PaintShapeType>? ShapeTypeChanged;
    public event Action<MediaColor>? BrushColorChanged;
    public event Action<MediaColor>? BoardColorChanged;
    public event Action<double>? BrushSizeChanged;
    public event Action<double>? EraserSizeChanged;
    public event Action? ClearRequested;

    public ICommand OpenBrushColorCommand { get; }
    public ICommand OpenBoardColorCommand { get; }

    public double BrushSize => BrushSizeSlider.Value;

    public PaintToolbarWindow()
    {
        InitializeComponent();
        OpenBrushColorCommand = new RelayCommand(OpenBrushColorDialog);
        OpenBoardColorCommand = new RelayCommand(OpenBoardColorDialog);
        DataContext = this;

        CursorButton.IsChecked = false;
        BrushButton.IsChecked = true;
        ShapeCombo.ItemsSource = new[]
        {
            PaintShapeType.Line,
            PaintShapeType.DashedLine,
            PaintShapeType.Rectangle,
            PaintShapeType.RectangleFill,
            PaintShapeType.Ellipse
        };
        ShapeCombo.SelectedIndex = 0;
        BrushSizeSlider.Value = 12;
        EraserSizeSlider.Value = 24;
    }

    private void OnModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button)
        {
            return;
        }
        ResetModeButtons(button);
        var mode = button == CursorButton ? PaintToolMode.Cursor
            : button == EraserButton ? PaintToolMode.Eraser
            : button == ShapeButton ? PaintToolMode.Shape
            : PaintToolMode.Brush;
        ModeChanged?.Invoke(mode);
    }

    private void OnShapeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ShapeCombo.SelectedItem is PaintShapeType type)
        {
            ShapeTypeChanged?.Invoke(type);
        }
    }

    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string hex)
        {
            var color = (MediaColor)ColorConverter.ConvertFromString(hex);
            BrushColorChanged?.Invoke(color);
        }
    }

    private void OnBrushSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        BrushSizeChanged?.Invoke(e.NewValue);
    }

    private void OnEraserSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        EraserSizeChanged?.Invoke(e.NewValue);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        ClearRequested?.Invoke();
    }

    private void OnBoardClick(object sender, RoutedEventArgs e)
    {
        if (BoardButton.IsChecked == true)
        {
            BoardColorChanged?.Invoke(Colors.White);
        }
        else
        {
            BoardColorChanged?.Invoke(Colors.Transparent);
        }
    }

    private void ResetModeButtons(ToggleButton active)
    {
        foreach (var button in new[] { CursorButton, BrushButton, EraserButton, ShapeButton })
        {
            button.IsChecked = button == active;
        }
    }

    private void OpenBrushColorDialog()
    {
        using var dialog = new System.Windows.Forms.ColorDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var color = MediaColor.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
            BrushColorChanged?.Invoke(color);
        }
    }

    private void OpenBoardColorDialog()
    {
        using var dialog = new System.Windows.Forms.ColorDialog();
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            var color = MediaColor.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
            BoardColorChanged?.Invoke(color);
        }
    }
}
