using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;

namespace ClassroomToolkit.App.Paint;

public partial class BoardColorDialog : Window
{
    private sealed record BoardColorOption(string Name, MediaColor Color);

    private static readonly BoardColorOption[] Options =
    {
        new("白板", Colors.White),
        new("黑板", Colors.Black),
        new("绿板", MediaColor.FromRgb(0x0E, 0x40, 0x20))
    };

    public MediaColor? SelectedColor { get; private set; }

    public BoardColorDialog()
    {
        InitializeComponent();
        BuildButtons();
    }

    private void BuildButtons()
    {
        foreach (var option in Options)
        {
            var button = new WpfButton
            {
                Content = option.Name,
                Width = 72,
                Height = 28,
                Margin = new Thickness(0, 0, 6, 0),
                Background = new SolidColorBrush(option.Color),
                Foreground = new SolidColorBrush(GetContrastingColor(option.Color)),
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(120, 0, 0, 0)),
                BorderThickness = new Thickness(1)
            };
            button.Click += (_, _) => SelectColor(option.Color);
            OptionsPanel.Children.Add(button);
        }
        if (OptionsPanel.Children.Count > 0
            && OptionsPanel.Children[^1] is FrameworkElement last)
        {
            last.Margin = new Thickness(0);
        }
    }

    private void SelectColor(MediaColor color)
    {
        SelectedColor = color;
        DialogResult = true;
    }

    private static MediaColor GetContrastingColor(MediaColor color)
    {
        var luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        return luminance >= 160 ? Colors.Black : Colors.White;
    }
}
