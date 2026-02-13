using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
                Content = new TextBlock
                {
                    Text = option.Name,
                    Foreground = new SolidColorBrush(GetContrastingColor(option.Color))
                },
                Width = 80,
                Height = 40,
                Margin = new Thickness(0, 0, 8, 0),
                Background = new SolidColorBrush(option.Color),
                Foreground = new SolidColorBrush(GetContrastingColor(option.Color)),
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(40, 0, 0, 0)),
                BorderThickness = new Thickness(1),
                FontWeight = FontWeights.SemiBold,
                Cursor = System.Windows.Input.Cursors.Hand,
                // 为了简单，我们直接设置圆角
                Template = CreateButtonTemplate(option.Color)
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

    private ControlTemplate CreateButtonTemplate(MediaColor color)
    {
        var template = new ControlTemplate(typeof(WpfButton));
        var border = new FrameworkElementFactory(typeof(Border));
        border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(WpfButton.BackgroundProperty));
        border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(WpfButton.BorderBrushProperty));
        border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(WpfButton.BorderThicknessProperty));
        border.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        
        var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
        presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);
        presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, System.Windows.VerticalAlignment.Center);

        border.AppendChild(presenter);
        
        template.VisualTree = border;
        return template;
    }

    private void SelectColor(MediaColor color)
    {
        SelectedColor = color;
        DialogResult = true;
    }

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }

    private static MediaColor GetContrastingColor(MediaColor color)
    {
        var luminance = 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        return luminance >= 160 ? Colors.Black : Colors.White;
    }
}

