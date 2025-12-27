using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using System.Windows.Media;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App.Paint;

public partial class PaintToolbarWindow : Window
{
    private bool _initializing;
    private readonly MediaColor[] _quickColors = new MediaColor[3];
    public event Action<PaintToolMode>? ModeChanged;
    public event Action<PaintShapeType>? ShapeTypeChanged;
    public event Action<MediaColor>? BrushColorChanged;
    public event Action<MediaColor>? BoardColorChanged;
    public event Action<double>? BrushSizeChanged;
    public event Action<double>? EraserSizeChanged;
    public event Action? ClearRequested;
    public event Action? UndoRequested;
    public event Action<byte>? BrushOpacityChanged;
    public event Action<byte>? BoardOpacityChanged;
    public event Action<string>? WpsModeChanged;
    public event Action<bool>? WpsWheelMappingChanged;
    public event Action<int, MediaColor>? QuickColorSlotChanged;

    public ICommand OpenBrushColorCommand { get; }
    public ICommand OpenBoardColorCommand { get; }
    public ICommand OpenQuickColor1Command { get; }
    public ICommand OpenQuickColor2Command { get; }
    public ICommand OpenQuickColor3Command { get; }

    public double BrushSize => BrushSizeSlider.Value;

    public PaintToolbarWindow()
    {
        InitializeComponent();
        OpenBrushColorCommand = new RelayCommand(OpenBrushColorDialog);
        OpenBoardColorCommand = new RelayCommand(OpenBoardColorDialog);
        OpenQuickColor1Command = new RelayCommand(() => OpenQuickColorDialog(0));
        OpenQuickColor2Command = new RelayCommand(() => OpenQuickColorDialog(1));
        OpenQuickColor3Command = new RelayCommand(() => OpenQuickColorDialog(2));
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
        BrushOpacitySlider.Value = 255;
        BoardOpacitySlider.Value = 0;
        SetQuickColorSlot(0, Colors.Black);
        SetQuickColorSlot(1, Colors.Red);
        SetQuickColorSlot(2, ColorFromHex("#1E90FF", Colors.DodgerBlue));
        WpsModeCombo.ItemsSource = new[] { "auto", "raw", "message" };
        WpsModeCombo.SelectedIndex = 0;
        WpsWheelCheck.IsChecked = false;
    }

    public void ApplySettings(AppSettings settings)
    {
        _initializing = true;
        try
        {
            BrushSizeSlider.Value = settings.BrushSize;
            EraserSizeSlider.Value = settings.EraserSize;
            BrushOpacitySlider.Value = settings.BrushOpacity;
            BoardOpacitySlider.Value = settings.BoardOpacity;
            SetQuickColorSlot(0, settings.QuickColor1);
            SetQuickColorSlot(1, settings.QuickColor2);
            SetQuickColorSlot(2, settings.QuickColor3);

            ShapeCombo.SelectedItem = settings.ShapeType;
            if (ShapeCombo.SelectedItem == null)
            {
                ShapeCombo.SelectedIndex = 0;
            }

            WpsModeCombo.SelectedItem = settings.WpsInputMode;
            if (WpsModeCombo.SelectedItem == null)
            {
                WpsModeCombo.SelectedIndex = 0;
            }

            WpsWheelCheck.IsChecked = settings.WpsWheelForward;
            BoardButton.IsChecked = settings.BoardOpacity > 0;
        }
        finally
        {
            _initializing = false;
        }
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
        if (_initializing)
        {
            return;
        }
        if (ShapeCombo.SelectedItem is PaintShapeType type)
        {
            ShapeTypeChanged?.Invoke(type);
        }
    }

    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button)
        {
            return;
        }
        var index = ResolveQuickColorIndex(button.Tag);
        if (!index.HasValue || index.Value < 0 || index.Value >= _quickColors.Length)
        {
            return;
        }
        BrushColorChanged?.Invoke(_quickColors[index.Value]);
    }

    private void OnBrushSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing)
        {
            return;
        }
        BrushSizeChanged?.Invoke(e.NewValue);
    }

    private void OnEraserSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing)
        {
            return;
        }
        EraserSizeChanged?.Invoke(e.NewValue);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        ClearRequested?.Invoke();
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        UndoRequested?.Invoke();
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

    private void OnBrushOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing)
        {
            return;
        }
        BrushOpacityChanged?.Invoke((byte)Math.Clamp((int)e.NewValue, 10, 255));
    }

    private void OnBoardOpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_initializing)
        {
            return;
        }
        BoardOpacityChanged?.Invoke((byte)Math.Clamp((int)e.NewValue, 0, 255));
    }

    private void OnWpsModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }
        if (WpsModeCombo.SelectedItem is string mode)
        {
            WpsModeChanged?.Invoke(mode);
        }
    }

    private void OnWpsWheelChanged(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }
        WpsWheelMappingChanged?.Invoke(WpsWheelCheck.IsChecked == true);
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

    private void OpenQuickColorDialog(int index)
    {
        using var dialog = new System.Windows.Forms.ColorDialog();
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }
        var color = MediaColor.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
        SetQuickColorSlot(index, color);
        QuickColorSlotChanged?.Invoke(index, color);
        BrushColorChanged?.Invoke(color);
    }

    private void SetQuickColorSlot(int index, MediaColor color)
    {
        if (index < 0 || index >= _quickColors.Length)
        {
            return;
        }
        _quickColors[index] = color;
        UpdateQuickColorButton(index, color);
    }

    private void UpdateQuickColorButton(int index, MediaColor color)
    {
        var button = index switch
        {
            0 => QuickColor1Button,
            1 => QuickColor2Button,
            2 => QuickColor3Button,
            _ => null
        };
        if (button == null)
        {
            return;
        }
        button.Background = new SolidColorBrush(color);
        button.Foreground = GetContrastingBrush(color);
    }

    private static Brush GetContrastingBrush(MediaColor color)
    {
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return luminance > 0.6 ? Brushes.Black : Brushes.White;
    }

    private static int? ResolveQuickColorIndex(object? tag)
    {
        if (tag is int index)
        {
            return index;
        }
        if (tag is string text && int.TryParse(text, out var parsed))
        {
            return parsed;
        }
        return null;
    }

    private static MediaColor ColorFromHex(string value, MediaColor fallback)
    {
        try
        {
            return (MediaColor)MediaColorConverter.ConvertFromString(value);
        }
        catch
        {
            return fallback;
        }
    }
}
