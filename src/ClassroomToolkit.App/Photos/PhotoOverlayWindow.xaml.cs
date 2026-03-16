using System.IO;
using System.Diagnostics;
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

        _currentPhotoPath = path;
        _currentStudentId = studentId?.Trim();
        NameText.Text = studentName ?? string.Empty;
        // 先隐藏姓名，避免在 Canvas 默认位置(0,0)即左上角闪现
        NameText.Visibility = Visibility.Collapsed;

        // 设置照片源
        PhotoImage.Source = bitmap;

        // 显示窗口
        Show();
        PhotoImage.Visibility = Visibility.Visible;

        // 强制更新布局以触发 SizeChanged
        UpdateLayout();

        // 在照片定位完成后再显示姓名，并重新定位到照片上方
        if (!string.IsNullOrWhiteSpace(studentName))
        {
            NameText.Visibility = Visibility.Visible;
            UpdateOverlayPositions();
        }

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

        // 定位关闭按钮：主入口固定在右下角
        var buttonMargin = 30.0;
        var buttonSize = 56.0;
        
        Canvas.SetLeft(CloseButtonLeft, photoLeft + buttonMargin);
        Canvas.SetTop(CloseButtonLeft, photoTop + photoHeight - buttonMargin - buttonSize);

        Canvas.SetLeft(CloseButtonRight, photoLeft + photoWidth - buttonMargin - buttonSize);
        Canvas.SetTop(CloseButtonRight, photoTop + photoHeight - buttonMargin - buttonSize);

        // 关闭提示文案位于底部居中，避免遮挡主体内容。
        CloseHintText.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        var hintLeft = Math.Max(16, (windowWidth - CloseHintText.DesiredSize.Width) / 2);
        var hintTop = Math.Max(16, photoTop + photoHeight - 26);
        Canvas.SetLeft(CloseHintText, hintLeft);
        Canvas.SetTop(CloseHintText, hintTop);
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
            
            // GC.Collect(); // Removed aggressive GC
            
            return bitmap;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"[PhotoOverlayWindow] Failed to load bitmap: {path}. Error: {ex.Message}");
            return null;
        }
    }

}

