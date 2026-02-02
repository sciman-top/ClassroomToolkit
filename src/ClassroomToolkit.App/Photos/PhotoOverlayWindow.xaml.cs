using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Shapes;
using ClassroomToolkit.App.Helpers;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App.Photos;

public partial class PhotoOverlayWindow : Window
{

    private readonly DispatcherTimer _autoCloseTimer;
    private string? _currentStudentId;
    private string? _currentPhotoPath;
    private IntPtr _hwnd;

    public event Action<string?>? PhotoClosed;

    public PhotoOverlayWindow()
    {
        InitializeComponent();
        ShowActivated = true;
        Focusable = false;
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoCloseTimer.Tick += OnAutoCloseTick;
        
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
        };
    }

    public void ShowPhoto(string path, string studentName, string studentId, int durationSeconds, Window? owner)
    {
        var bitmap = LoadBitmap(path);
        if (bitmap == null)
        {
            Hide();
            return;
        }

        // 清除缓存
        PhotoImage.Source = null;
        NameText.Text = string.Empty;
        PhotoImage.Visibility = Visibility.Collapsed;
        LoadingMask.Visibility = Visibility.Collapsed;
        
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        _currentPhotoPath = path;
        _currentStudentId = studentId?.Trim();
        NameText.Text = studentName ?? string.Empty;
        NameText.Visibility = string.IsNullOrWhiteSpace(NameText.Text) ? Visibility.Collapsed : Visibility.Visible;

        // 设置照片源
        PhotoImage.Source = bitmap;

        // 显示窗口
        Show();
        PhotoImage.Visibility = Visibility.Visible;

        // 强制更新布局以触发 SizeChanged
        UpdateLayout();

        if (durationSeconds > 0)
        {
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(durationSeconds);
            _autoCloseTimer.Start();
        }
        else
        {
            _autoCloseTimer.Stop();
        }
    }

    private void OnCanvasSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 更新背景矩形大小
        BackgroundRect.Width = e.NewSize.Width;
        BackgroundRect.Height = e.NewSize.Height;
        
        // 更新遮挡层大小
        LoadingMask.Width = e.NewSize.Width;
        LoadingMask.Height = e.NewSize.Height;
        
        // 重新计算布局
        UpdateOverlayPositions();
    }

    private void OnPhotoSizeChanged(object sender, SizeChangedEventArgs e)
    {
        // 照片大小改变时重新计算位置
        UpdateOverlayPositions();
    }

    private void UpdateOverlayPositions()
    {
        if (PhotoImage.Source == null || PhotoImage.ActualWidth == 0 || PhotoImage.ActualHeight == 0)
        {
            return;
        }

        var windowWidth = RootCanvas.ActualWidth;
        var windowHeight = RootCanvas.ActualHeight;
        var photoWidth = PhotoImage.ActualWidth;
        var photoHeight = PhotoImage.ActualHeight;

        // 照片居中显示
        var photoLeft = (windowWidth - photoWidth) / 2;
        var photoTop = (windowHeight - photoHeight) / 2;

        Canvas.SetLeft(PhotoImage, photoLeft);
        Canvas.SetTop(PhotoImage, photoTop);

        // 定位姓名：在照片顶部居中，左右各留 30px
        if (NameText.Visibility == Visibility.Visible)
        {
            NameText.MaxWidth = Math.Max(100, photoWidth - 60);
            NameText.Measure(new WpfSize(NameText.MaxWidth, double.PositiveInfinity));
            var nameWidth = NameText.DesiredSize.Width;
            Canvas.SetLeft(NameText, photoLeft + (photoWidth - nameWidth) / 2);
            Canvas.SetTop(NameText, photoTop + 30);
        }

        // 定位关闭按钮：在照片底部左右角
        var buttonMargin = 30.0;
        var buttonSize = 56.0;
        
        Canvas.SetLeft(CloseButtonLeft, photoLeft + buttonMargin);
        Canvas.SetTop(CloseButtonLeft, photoTop + photoHeight - buttonMargin - buttonSize);

        Canvas.SetLeft(CloseButtonRight, photoLeft + photoWidth - buttonMargin - buttonSize);
        Canvas.SetTop(CloseButtonRight, photoTop + photoHeight - buttonMargin - buttonSize);
    }

    public void CloseOverlay()
    {
        _autoCloseTimer.Stop();
        ClearPhotoCache();
        Hide();
    }

    private void OnAutoCloseTick(object? sender, EventArgs e)
    {
        CloseOverlay();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private void ClearPhotoCache()
    {
        PhotoImage.Source = null;
        NameText.Text = string.Empty;
        NameText.Visibility = Visibility.Collapsed;
        var studentId = _currentStudentId;
        _currentStudentId = null;
        _currentPhotoPath = null;
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            PhotoClosed?.Invoke(studentId);
        }
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            var uri = new Uri(path, UriKind.Absolute);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = uri;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();
            
            GC.Collect();
            
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

}
