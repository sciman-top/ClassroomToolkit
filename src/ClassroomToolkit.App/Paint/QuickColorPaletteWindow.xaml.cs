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
        new("黑", Colors.Black),
        new("红", Colors.Red),
        new("蓝", MediaColor.FromRgb(0x1E, 0x90, 0xFF)),
        new("绿", MediaColor.FromRgb(0x24, 0xB4, 0x7E)),
        new("黄", Colors.Yellow),
        new("橙", Colors.Orange),
        new("紫", MediaColor.FromRgb(0x80, 0x00, 0x80)),
        new("白", Colors.White)
    };

    public MediaColor? SelectedColor { get; private set; }

    public QuickColorPaletteWindow()
    {
        InitializeComponent();
        BuildButtons();
        Deactivated += (_, _) =>
        {
            if (IsVisible && SelectedColor == null)
            {
                Close();
            }
        };
    }

    private void BuildButtons()
    {
        foreach (var option in Options)
        {
            var button = new System.Windows.Controls.Button
            {
                Width = 22,
                Height = 22,
                Margin = new Thickness(2, 0, 2, 0),
                Background = new SolidColorBrush(option.Color),
                BorderBrush = new SolidColorBrush(GetContrastBorderColor(option.Color)),
                BorderThickness = new Thickness(IsDarkColor(option.Color) ? 2 : 1),
                ToolTip = option.Name,
                Style = (Style)FindResource("ColorBlockButtonStyle")
            };
            button.Click += (_, _) => SelectColor(option.Color);
            OptionsPanel.Children.Add(button);
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
