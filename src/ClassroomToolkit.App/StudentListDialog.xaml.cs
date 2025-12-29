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
        // 计算需要的卡片布局 - 已更新为美化后的尺寸
        const double itemWidth = 150;  // WrapPanel ItemWidth
        const double itemHeight = 60;  // WrapPanel ItemHeight
        // 注意：WrapPanel 内部是流式布局，Margin 是在 ItemTemplate 里的 Button 上设置的，
        // 但 WrapPanel 计算时只看 ItemWidth/Height。
        // 不过由于我们设置了 WrapPanel 的 ItemWidth/Height 属性，它会强制每个 item 占用这个空间。
        
        var scrollMargin = (ListScrollViewer?.Margin.Top ?? 10) + (ListScrollViewer?.Margin.Bottom ?? 10);
        // 标题栏高度 50，底部高度约 52 (Padding 10*2 + Content 32)
        const double headerHeight = 50; 
        const double footerHeight = 54; 
        const double windowBorderMargin = 30; // 窗口阴影外边距 (15*2)

        // 计算可用宽度（减去边距）
        var rawWidth = ActualWidth > 0 ? ActualWidth : Width;
        var listAvailableWidth = rawWidth - windowBorderMargin - 20; // 20 is ScrollViewer margin left+right
        
        // 计算每行可以放多少个卡片
        var columns = Math.Max(1, (int)(listAvailableWidth / itemWidth));

        // 计算需要多少行
        var rows = Math.Max(1, (int)Math.Ceiling(_students.Count / (double)columns));

        // 计算内容高度
        var contentHeight = rows * itemHeight;
        var minContentHeight = itemHeight;
        
        // 限制最大高度
        var maxWindowHeight = Math.Max(400, SystemParameters.WorkArea.Height - 100);
        var maxContentHeight = Math.Max(minContentHeight, maxWindowHeight - headerHeight - footerHeight - windowBorderMargin - scrollMargin);
        
        contentHeight = Math.Clamp(contentHeight, minContentHeight, maxContentHeight);

        // 计算总高度
        var totalHeight = headerHeight + contentHeight + footerHeight + windowBorderMargin + scrollMargin;

        // 调整窗口高度
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