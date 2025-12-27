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

    public static readonly DependencyProperty StudentColumnCountProperty =
        DependencyProperty.Register(nameof(StudentColumnCount), typeof(int), typeof(StudentListDialog), new PropertyMetadata(10));

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

    public int StudentColumnCount
    {
        get => (int)GetValue(StudentColumnCountProperty);
        private set => SetValue(StudentColumnCountProperty, value);
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
        var maxColumnsByWidth = Math.Max(1, Math.Min(10,
            (int)Math.Floor((workArea.Width * 0.9 - 40d + hSpacing) / (StudentButtonWidth + hSpacing))));
        var idealColumns = (int)Math.Ceiling(Math.Sqrt(count));
        var columns = Math.Max(1, Math.Min(maxColumnsByWidth, idealColumns));
        StudentColumnCount = columns;

        var totalRows = Math.Max(1, (int)Math.Ceiling(count / (double)columns));
        var preferredWidth = Math.Min(workArea.Width * 0.9, StudentButtonWidth * columns + hSpacing * (columns - 1) + 40d);
        var extraHeight = 80d;
        var desiredHeight = totalRows * StudentButtonHeight + Math.Max(0, totalRows - 1) * vSpacing + extraHeight;
        var preferredHeight = Math.Min(workArea.Height * 0.85, Math.Max(240d, desiredHeight));
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
