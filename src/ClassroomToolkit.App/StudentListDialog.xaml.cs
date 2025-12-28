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

        // 计算需要的卡片布局
        const double itemWidth = 120;  // WrapPanel ItemWidth
        const double itemHeight = 48;  // WrapPanel ItemHeight
        const double itemMargin = 12;  // 卡片间距 (6 * 2)
        const double scrollMargin = 32; // ScrollViewer Margin (16 * 2)
        const double headerHeight = 40;  // 标题栏高度
        const double footerHeight = 60;  // 底部高度
        const double minContentHeight = 300;  // 最小内容高度
        const double maxContentHeight = 700;  // 最大内容高度

        // 计算可用宽度（减去边距）
        var availableWidth = Width - scrollMargin;

        // 计算每行可以放多少个卡片
        var columns = Math.Max(1, (int)(availableWidth / (itemWidth + itemMargin)));

        // 计算需要多少行
        var rows = (int)Math.Ceiling(_students.Count / (double)columns);

        // 计算内容高度
        var contentHeight = rows * (itemHeight + itemMargin);
        contentHeight = Math.Clamp(contentHeight, minContentHeight, maxContentHeight);

        // 计算总高度
        var totalHeight = headerHeight + contentHeight + footerHeight + scrollMargin;

        // 调整窗口高度（保持宽度不变）
        Height = totalHeight;
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
