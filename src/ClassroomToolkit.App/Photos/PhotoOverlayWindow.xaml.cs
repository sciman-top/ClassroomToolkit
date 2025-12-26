using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ClassroomToolkit.App.Photos;

public partial class PhotoOverlayWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;

    public PhotoOverlayWindow()
    {
        InitializeComponent();
        ShowActivated = false;
        Focusable = false;
        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoCloseTimer.Tick += OnAutoCloseTick;
    }

    public void ShowPhoto(string path, string studentName, int durationSeconds, Window? owner)
    {
        var bitmap = LoadBitmap(path);
        if (bitmap == null)
        {
            Hide();
            return;
        }
        NameText.Text = studentName ?? string.Empty;
        NameText.Visibility = string.IsNullOrWhiteSpace(NameText.Text) ? Visibility.Collapsed : Visibility.Visible;

        var screen = ResolveScreen(owner);
        var maxWidth = screen.Width * 0.8;
        var maxHeight = screen.Height * 0.8;
        var scale = Math.Min(1.0, Math.Min(maxWidth / bitmap.PixelWidth, maxHeight / bitmap.PixelHeight));
        var targetWidth = Math.Max(1, bitmap.PixelWidth * scale);
        var targetHeight = Math.Max(1, bitmap.PixelHeight * scale);

        PhotoImage.Source = bitmap;
        PhotoImage.Width = targetWidth;
        PhotoImage.Height = targetHeight;

        Width = targetWidth;
        Height = targetHeight;
        Left = screen.X + (screen.Width - targetWidth) / 2;
        Top = screen.Y + (screen.Height - targetHeight) / 2;

        Show();
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
        Hide();
    }

    private void OnAutoCloseTick(object? sender, EventArgs e)
    {
        _autoCloseTimer.Stop();
        Hide();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private static BitmapImage? LoadBitmap(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
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
}
