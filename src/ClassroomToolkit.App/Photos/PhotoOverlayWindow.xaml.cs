using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;

namespace ClassroomToolkit.App.Photos;

public partial class PhotoOverlayWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExNoActivate = 0x08000000;
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
            // 不应用 WS_EX_NOACTIVATE 以确保窗口能够正常获得焦点和用户交互
            // ApplyNoActivate();
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
        
        // 先隐藏 Image 控件，避免闪现上一个学生的照片
        PhotoImage.Visibility = Visibility.Collapsed;
        PhotoImage.Source = null;
        
        _currentPhotoPath = path;
        _currentStudentId = studentId?.Trim();
        NameText.Text = studentName ?? string.Empty;
        NameText.Visibility = string.IsNullOrWhiteSpace(NameText.Text) ? Visibility.Collapsed : Visibility.Visible;

        var screen = ResolveScreen(owner);
        var maxWidth = screen.Width * 0.8;
        var maxHeight = screen.Height * 0.8;
        var scale = Math.Min(1.0, Math.Min(maxWidth / bitmap.PixelWidth, maxHeight / bitmap.PixelHeight));
        var targetWidth = Math.Max(1, bitmap.PixelWidth * scale);
        var targetHeight = Math.Max(1, bitmap.PixelHeight * scale);

        // 设置新的照片内容
        PhotoImage.Source = bitmap;
        PhotoImage.Width = targetWidth;
        PhotoImage.Height = targetHeight;

        Width = targetWidth;
        Height = targetHeight;
        Left = screen.X + (screen.Width - targetWidth) / 2;
        Top = screen.Y + (screen.Height - targetHeight) / 2;
        WindowPlacementHelper.EnsureVisible(this);

        Show();
        
        // 确保新图片加载完成后再显示 Image 控件
        PhotoImage.Visibility = Visibility.Visible;
        
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

    private void ClearVisualContent()
    {
        // 使用透明画刷替换当前图片，避免闪现上一个学生的照片
        PhotoImage.Source = new DrawingBrush {
            Drawing = new GeometryDrawing {
                Brush = System.Windows.Media.Brushes.Transparent,
                Geometry = new RectangleGeometry(new Rect(0, 0, 1, 1))
            }
        };
        
        PhotoImage.Width = double.NaN;
        PhotoImage.Height = double.NaN;
        NameText.Text = string.Empty;
        NameText.Visibility = Visibility.Collapsed;
    }

    private void ClearPhotoCache()
    {
        PhotoImage.Source = null;
        PhotoImage.Width = double.NaN;
        PhotoImage.Height = double.NaN;
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
            // 强制清除可能的缓存
            var uri = new Uri(path, UriKind.Absolute);
            
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = uri;
            // 强制重新加载，忽略任何缓存
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static System.Drawing.Rectangle ResolveScreen(Window? owner)
    {
        if (owner != null)
        {
            var handle = new WindowInteropHelper(owner).Handle;
            if (handle != IntPtr.Zero)
            {
                return System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
            }
        }
        return System.Windows.Forms.Screen.PrimaryScreen?.WorkingArea ?? new System.Drawing.Rectangle(0, 0, 1280, 720);
    }
    
    private void ApplyNoActivate()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var exStyle = GetWindowLong(_hwnd, GwlExstyle);
        SetWindowLong(_hwnd, GwlExstyle, exStyle | WsExNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
