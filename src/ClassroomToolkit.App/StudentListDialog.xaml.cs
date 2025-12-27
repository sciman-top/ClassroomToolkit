using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.App.Helpers;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App;

public partial class StudentListDialog : Window
{
    private const double StudentNameFontSize = 16d;
    public static readonly DependencyProperty StudentButtonWidthProperty =
        DependencyProperty.Register(nameof(StudentButtonWidth), typeof(double), typeof(StudentListDialog), new PropertyMetadata(120d));

    public static readonly DependencyProperty StudentButtonHeightProperty =
        DependencyProperty.Register(nameof(StudentButtonHeight), typeof(double), typeof(StudentListDialog), new PropertyMetadata(34d));

    private readonly IReadOnlyList<StudentListItem> _students;

    public StudentListDialog(IReadOnlyList<StudentListItem> students)
    {
        InitializeComponent();
        _students = students ?? Array.Empty<StudentListItem>();
        StudentItems.ItemsSource = _students;
        Loaded += OnLoaded;
    }

    public double StudentButtonWidth
    {
        get => (double)GetValue(StudentButtonWidthProperty);
        private set => SetValue(StudentButtonWidthProperty, value);
    }

    public double StudentButtonHeight
    {
        get => (double)GetValue(StudentButtonHeightProperty);
        private set => SetValue(StudentButtonHeightProperty, value);
    }

    public int? SelectedIndex { get; private set; }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var typeface = new Typeface(FontFamily, FontStyle, FontWeights.Bold, FontStretch);
        var maxTextWidth = 120d;
        var maxTextHeight = 0d;
        foreach (var item in _students)
        {
            var text = item.DisplayText;
            var size = MeasureTextSize(text, typeface, StudentNameFontSize, dpi);
            if (size.Width > maxTextWidth)
            {
                maxTextWidth = size.Width;
            }
            if (size.Height > maxTextHeight)
            {
                maxTextHeight = size.Height;
            }
        }

        var dotPadding = 20d;
        var minWidth = Math.Max(120d, maxTextWidth + dotPadding);
        var workArea = SystemParameters.WorkArea;
        var itemMargin = 3d;
        var columns = 10;
        var rootWidthMargin = Root.Margin.Left + Root.Margin.Right;
        var maxWidthPerButton = (workArea.Width * 0.95 - rootWidthMargin) / columns - itemMargin * 2d;
        StudentButtonWidth = Math.Min(minWidth, Math.Max(96d, maxWidthPerButton));
        var paddingY = 6d;
        StudentButtonHeight = Math.Max(42d, maxTextHeight + paddingY * 2d);

        var count = Math.Max(1, _students.Count);
        var totalRows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));
        var preferredWidth = columns * (StudentButtonWidth + itemMargin * 2d) + rootWidthMargin;
        var extraHeight = CalculateExtraHeight();
        var rowHeight = StudentButtonHeight + itemMargin * 2d;
        var desiredHeight = totalRows * rowHeight + extraHeight;
        Width = Math.Max(200d, preferredWidth);
        Height = Math.Max(200d, desiredHeight);
        WindowPlacementHelper.EnsureVisible(this);
    }

    private double CalculateExtraHeight()
    {
        var rootMargin = Root.Margin.Top + Root.Margin.Bottom;
        var hintHeight = HintText?.ActualHeight ?? 18d;
        var hintMargin = HintText != null ? HintText.Margin.Top + HintText.Margin.Bottom : 0d;
        var footerHeight = FooterPanel?.ActualHeight ?? 30d;
        var footerMargin = FooterPanel != null ? FooterPanel.Margin.Top + FooterPanel.Margin.Bottom : 0d;
        return rootMargin + hintHeight + hintMargin + footerHeight + footerMargin + 6d;
    }

    private static WpfSize MeasureTextSize(string text, Typeface typeface, double fontSize, double pixelsPerDip)
    {
        var formatted = new FormattedText(
            text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            System.Windows.Media.Brushes.Black,
            pixelsPerDip);
        return new WpfSize(formatted.Width, formatted.Height);
    }

    private void OnStudentClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is StudentListItem item)
        {
            SelectedIndex = item.Index;
            DialogResult = true;
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}
