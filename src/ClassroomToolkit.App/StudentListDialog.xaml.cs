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
            if (!item.Called)
            {
                text = $"{text} ●";
            }
            var width = MeasureTextWidth(text, typeface, FontSize, dpi);
            if (width > maxText)
            {
                maxText = width;
            }
        }

        var minWidth = Math.Max(120d, maxText + 24d);
        var workArea = SystemParameters.WorkArea;
        var maxWidthPerButton = Math.Max(96d, (workArea.Width * 0.9 - 40d) / 10d);
        StudentButtonWidth = Math.Min(minWidth, maxWidthPerButton);
        StudentButtonHeight = Math.Max(34d, FontSize + 14d);

        var totalRows = Math.Max(1, (int)Math.Ceiling(_students.Count / 10d));
        var hSpacing = 6d;
        var vSpacing = 6d;
        var preferredWidth = Math.Min(workArea.Width * 0.9, StudentButtonWidth * 10d + hSpacing * 9d + 40d);
        var preferredHeight = Math.Min(
            workArea.Height * 0.85,
            totalRows * StudentButtonHeight + Math.Max(0, totalRows - 1) * vSpacing + 90d);
        Width = preferredWidth;
        Height = preferredHeight;
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
