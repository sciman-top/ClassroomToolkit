using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App;

public partial class StudentListDialog : Window
{
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
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);
        var maxText = 120d;
        foreach (var item in _students)
        {
            var text = item.DisplayText;
            var width = MeasureTextWidth(text, typeface, FontSize, dpi);
            if (width > maxText)
            {
                maxText = width;
            }
        }

        var dotPadding = 20d;
        var minWidth = Math.Max(120d, maxText + dotPadding);
        var workArea = SystemParameters.WorkArea;
        var maxWidthPerButton = Math.Max(96d, (workArea.Width * 0.9 - 40d) / 10d);
        StudentButtonWidth = Math.Min(minWidth, maxWidthPerButton);
        StudentButtonHeight = Math.Max(34d, FontSize + 16d);

        var count = Math.Max(1, _students.Count);
        var hSpacing = 6d;
        var vSpacing = 6d;
        var columns = 10;
        var totalRows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));
        var preferredWidth = StudentButtonWidth * columns + hSpacing * (columns - 1) + 32d;
        var extraHeight = 56d;
        var desiredHeight = totalRows * StudentButtonHeight + Math.Max(0, totalRows - 1) * vSpacing + extraHeight;
        Width = Math.Max(200d, preferredWidth);
        Height = Math.Max(200d, desiredHeight);
        WindowPlacementHelper.EnsureVisible(this);
    }

    private static double MeasureTextWidth(string text, Typeface typeface, double fontSize, double pixelsPerDip)
    {
        var formatted = new FormattedText(
            text ?? string.Empty,
            CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            typeface,
            fontSize,
            System.Windows.Media.Brushes.Black,
            pixelsPerDip);
        return formatted.Width;
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
