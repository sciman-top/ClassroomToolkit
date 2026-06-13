using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;
using WpfOrientation = System.Windows.Controls.Orientation;

namespace ClassroomToolkit.App.Paint;

public partial class QuickColorPaletteWindow : Window
{
    private sealed record ColorOption(string Name, MediaColor Color);
    private sealed record BrushSizeOption(int Index, double Size);

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
    public int? SelectedBrushSizeIndex { get; private set; }

    public QuickColorPaletteWindow()
        : this(Array.Empty<double>(), selectedBrushSizeIndex: -1)
    {
    }

    public QuickColorPaletteWindow(IReadOnlyList<double>? brushSizes, int selectedBrushSizeIndex)
    {
        InitializeComponent();
        BuildButtons();
        BuildBrushSizeButtons(brushSizes, selectedBrushSizeIndex);
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

    private void BuildBrushSizeButtons(IReadOnlyList<double>? brushSizes, int selectedBrushSizeIndex)
    {
        var options = NormalizeBrushSizeOptions(brushSizes);
        foreach (var option in options)
        {
            var isSelected = option.Index == selectedBrushSizeIndex;
            var button = new System.Windows.Controls.Button
            {
                MinWidth = 66,
                Height = 48,
                Margin = new Thickness(4, 0, 4, 0),
                Padding = new Thickness(10, 5, 10, 5),
                Background = isSelected
                    ? new SolidColorBrush(MediaColor.FromRgb(0xE6, 0xF6, 0xFF))
                    : MediaBrushes.White,
                BorderBrush = isSelected
                    ? new SolidColorBrush(MediaColor.FromRgb(0x0E, 0x74, 0xB8))
                    : MediaBrushes.Gray,
                BorderThickness = new Thickness(isSelected ? 3 : 1),
                Foreground = MediaBrushes.Black,
                FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal,
                ToolTip = $"选择{Math.Round(option.Size)}px笔画",
                Tag = option.Index,
                Content = BuildBrushSizePreview(option.Size, isSelected)
            };
            button.Click += OnBrushSizeButtonClick;
            BrushSizeOptionsPanel.Children.Add(button);
        }
    }

    private static BrushSizeOption[] NormalizeBrushSizeOptions(IReadOnlyList<double>? brushSizes)
    {
        if (brushSizes is { Count: >= 3 })
        {
            return new[]
            {
                new BrushSizeOption(0, NormalizeBrushSize(brushSizes[0], fallback: 6)),
                new BrushSizeOption(1, NormalizeBrushSize(brushSizes[1], fallback: 12)),
                new BrushSizeOption(2, NormalizeBrushSize(brushSizes[2], fallback: 24))
            };
        }

        return new[]
        {
            new BrushSizeOption(0, 6),
            new BrushSizeOption(1, 12),
            new BrushSizeOption(2, 24)
        };
    }

    private static double NormalizeBrushSize(double size, double fallback)
    {
        if (double.IsNaN(size) || double.IsInfinity(size))
        {
            var safeFallback = double.IsNaN(fallback) || double.IsInfinity(fallback) ? 12.0 : fallback;
            return Math.Clamp(safeFallback, 1.0, 50.0);
        }

        return Math.Clamp(size, 1.0, 50.0);
    }

    private static StackPanel BuildBrushSizePreview(double size, bool isSelected)
    {
        var diameter = Math.Clamp(size, 5.0, 28.0);
        return new StackPanel
        {
            Orientation = WpfOrientation.Horizontal,
            HorizontalAlignment = WpfHorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                new System.Windows.Shapes.Ellipse
                {
                    Width = diameter,
                    Height = diameter,
                    Fill = MediaBrushes.Black,
                    Stroke = isSelected ? MediaBrushes.DodgerBlue : MediaBrushes.Transparent,
                    StrokeThickness = isSelected ? 3 : 0,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 0, 5, 0)
                },
                new TextBlock
                {
                    Text = $"{Math.Round(size)}",
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = MediaBrushes.Black,
                    FontWeight = isSelected ? FontWeights.SemiBold : FontWeights.Normal
                }
            }
        };
    }

    private void OnColorButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: MediaColor color })
        {
            return;
        }

        SelectColor(color);
    }

    private void OnBrushSizeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: int index })
        {
            return;
        }

        SelectBrushSize(index);
    }

    private void OnWindowDeactivated(object? sender, EventArgs e)
    {
        if (IsVisible && SelectedColor == null && SelectedBrushSizeIndex == null)
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

        foreach (var child in BrushSizeOptionsPanel.Children)
        {
            if (child is System.Windows.Controls.Button button)
            {
                button.Click -= OnBrushSizeButtonClick;
            }
        }
    }

    private void SelectColor(MediaColor color)
    {
        SelectedColor = color;
        DialogResult = true;
    }

    private void SelectBrushSize(int index)
    {
        SelectedBrushSizeIndex = index;
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
