using System.Globalization;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Models;
using ClassroomToolkit.App.Helpers;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App;

public partial class StudentListDialog : Window
{
    private readonly IReadOnlyList<StudentListItem> _students;

    public StudentListDialog(IReadOnlyList<StudentListItem> students)
    {
        InitializeComponent();
        _students = students ?? Array.Empty<StudentListItem>();
        StudentItems.ItemsSource = _students;
        Loaded += OnWindowLoaded;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        // 根据人数自动调整窗口高度
        AdjustWindowSize();

        WindowPlacementHelper.EnsureVisible(this);
    }

    /// <summary>
    /// 根据学生人数自动调整窗口大小
    /// </summary>
    private void AdjustWindowSize()
    {
        if (_students.Count == 0) return;

        const double itemHeight = 56;  // 对应 XAML 中的 ItemHeight
        const double headerHeight = 56;
        const double footerHeight = 50;
        const double windowBorderMargin = 20;

        // 一行 10 人
        const int columns = 10;
        var rows = (int)Math.Ceiling(_students.Count / (double)columns);

        // 计算所需内容高度
        var contentHeight = rows * itemHeight;

        // 计算理想的总高度
        var idealHeight = headerHeight + contentHeight + footerHeight + windowBorderMargin + 20;

        // 限制最大高度为屏幕高度的 85%
        var screenHeight = SystemParameters.WorkArea.Height;
        var screenWidth = SystemParameters.WorkArea.Width;
        var maxHeight = screenHeight * 0.85;

        // 设置初始窗口大小，但允许用户调整
        Width = 1120;
        Height = Math.Min(idealHeight, maxHeight);

        // 设置最小尺寸以确保内容可见
        MinWidth = 600;
        MinHeight = Math.Min(400, maxHeight);

        // 居中显示
        Left = (screenWidth - Width) / 2;
        Top = (screenHeight - Height) / 2;
    }

    public int? SelectedIndex { get; private set; }

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

    private void OnTitleBarDrag(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
        {
            DragMove();
        }
    }
}