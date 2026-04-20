using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint;

public partial class QuickColorPaletteWindow : Window
{
    private sealed record ColorOption(string Name, MediaColor Color);

    private static readonly ColorOption[] Options =
    {
        new("黑色", Colors.Black),
        new("红色", Colors.Red),
        new("蓝色", MediaColor.FromRgb(0x1E, 0x90, 0xFF)),
        new("绿色", MediaColor.FromRgb(0x24, 0xB4, 0x7E)),
        new("黄色", Colors.Yellow),
        new("橙色", Colors.Orange),
        new("紫色", MediaColor.FromRgb(0x80, 0x00, 0x80)),
        new("白色", Colors.White)
    };

    public MediaColor? SelectedColor { get; private set; }

    public QuickColorPaletteWindow()
    {
        InitializeComponent();
        BuildButtons();
        Deactivated += OnWindowDeactivated;
        Closed += OnWindowClosed;
    }

    private void BuildButtons()
    {
        foreach (var option in Options)
        {
            var button = new System.Windows.Controls.Button
            {
                Width = 36,
                Height = 36,
                Margin = new Thickness(4, 0, 4, 0),
                Background = new SolidColorBrush(option.Color),
                BorderBrush = new SolidColorBrush(GetContrastBorderColor(option.Color)),
                BorderThickness = new Thickness(IsDarkColor(option.Color) ? 2 : 1),
                ToolTip = $"选择{option.Name}",
                Tag = option.Color,
                Style = (Style)FindResource("Style_ColorPaletteButton")
            };
            button.Click += OnColorButtonClick;
            OptionsPanel.Children.Add(button);
        }
    }

    private void OnColorButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: MediaColor color })
        {
            return;
        }

        SelectColor(color);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (IsVisible && SelectedColor == null)
        {
            Close();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        Deactivated -= OnWindowDeactivated;
        Closed -= OnWindowClosed;

        foreach (var child in OptionsPanel.Children)
        {
            if (child is System.Windows.Controls.Button button)
            {
                button.Click -= OnColorButtonClick;
            }
        }
    }

    private void SelectColor(MediaColor color)
    {
        SelectedColor = color;
        DialogResult = true;
    }

    private static MediaColor GetContrastBorderColor(MediaColor color)
    {
        return IsDarkColor(color)
            ? MediaColor.FromArgb(220, 255, 255, 255)
            : MediaColor.FromArgb(140, 0, 0, 0);
    }

    private static bool IsDarkColor(MediaColor color)
    {
        var luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        return luminance < 70;
    }
}
