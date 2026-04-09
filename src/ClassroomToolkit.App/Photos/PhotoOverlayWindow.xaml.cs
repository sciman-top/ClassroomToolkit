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
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using IOPath = System.IO.Path;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App.Photos;

public partial class PhotoOverlayWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly System.Windows.Media.Brush _defaultLoadingMaskBrush;
    private DateTime _autoCloseDueUtc;
    private string? _currentStudentId;
    private string? _currentPhotoPath;
    private IntPtr _hwnd;
    private int _photoLoadRequestId;
    private string? _cachedBitmapPath;
    private BitmapSource? _cachedBitmap;
    private static readonly SolidColorBrush OpaqueFrameGuardBrush = CreateOpaqueFrameGuardBrush();

    public event Action<string?>? PhotoClosed;

    public PhotoOverlayWindow()
    {
        InitializeComponent();
        ShowActivated = true;
        Focusable = false;
        _autoCloseTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoCloseTimer.Tick += OnAutoCloseTick;
        _defaultLoadingMaskBrush = LoadingMask.Background;
        SourceInitialized += OnOverlaySourceInitialized;
        Closed += OnOverlayClosed;
    }

    private void OnOverlaySourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _photoLoadRequestId);
        _autoCloseTimer.Stop();
        _autoCloseTimer.Tick -= OnAutoCloseTick;
        SourceInitialized -= OnOverlaySourceInitialized;
        Closed -= OnOverlayClosed;
        ClearPhotoCache(enterHideGuardState: false);
    }

    public void ShowPhoto(string path, string studentName, string studentId, int durationSeconds, Window? owner)
    {
        var requestId = Interlocked.Increment(ref _photoLoadRequestId);
        _autoCloseTimer.Stop();
        var deferShowUntilBitmapReady = !IsVisible;
        var normalizedStudentId = studentId?.Trim();
        PhotoOverlayDiagnostics.Log(
            "show-start",
            $"req={requestId} path={IOPath.GetFileName(path)} studentId={normalizedStudentId ?? string.Empty} duration={durationSeconds} same={IsShowingSamePhoto(path)} visible={IsVisible} loading={LoadingMask.Visibility}");

        if (IsShowingSamePhoto(path))
        {
            Opacity = 1.0;
            _currentPhotoPath = path;
            _currentStudentId = normalizedStudentId;
            UpdateStudentName(studentName, visible: !string.IsNullOrWhiteSpace(studentName));
            UpdateOverlayPositions();
            UpdateAutoCloseTimer(durationSeconds);
            EnsureOverlayVisible();
            PhotoOverlayDiagnostics.Log(
                "show-reuse",
                $"req={requestId} path={IOPath.GetFileName(path)} duration={durationSeconds} timer=reset visible={IsVisible}");
            return;
        }

        _currentPhotoPath = path;
        _currentStudentId = normalizedStudentId;
        UpdateStudentName(studentName, visible: !string.IsNullOrWhiteSpace(studentName));
        PhotoOverlayDiagnostics.Log(
            "show-reset",
            $"req={requestId} path={IOPath.GetFileName(path)} duration={durationSeconds} clearing-old-frame");
        // 显示窗口前先透明，避免系统复用上一帧导致旧图闪现。
        Opacity = 0.0;

        // 先清空上一张图并进入遮挡态，避免窗口可见时先闪出旧图。
        PhotoImage.Source = null;
        PhotoImage.Visibility = Visibility.Collapsed;
        LoadingMask.Visibility = Visibility.Visible;
        LoadingMask.Background = OpaqueFrameGuardBrush;
        // Force immediate visual state commit while this window is still on-screen
        // so the previous frame is less likely to flash for a single composition frame.
        UpdateLayout();
        if (!deferShowUntilBitmapReady)
        {
            EnsureOverlayVisible();
            PhotoOverlayDiagnostics.Log(
                "show-visible",
                $"req={requestId} path={IOPath.GetFileName(path)} visible={IsVisible} topmost={Topmost} state={WindowState}");
        }
        else
        {
            ApplyWindowedBounds();
            PhotoOverlayDiagnostics.Log(
                "show-deferred",
                $"req={requestId} path={IOPath.GetFileName(path)} visible={IsVisible}");
        }
        // 窗口复显后再次施加透明保护，避免复显首帧复用旧合成帧。
        Opacity = 0.0;
        if (TryGetCachedBitmap(path, out var cachedBitmap))
        {
            PhotoOverlayDiagnostics.Log(
                "cache-hit",
                $"req={requestId} path={IOPath.GetFileName(path)}");
            ApplyLoadedBitmap(
                requestId,
                cachedBitmap,
                studentName,
                durationSeconds,
                hideWhenFailed: false,
                ensureVisibleOnApply: deferShowUntilBitmapReady);
            return;
        }

        _ = SafeTaskRunner.Run(
            "PhotoOverlayWindow.ShowPhoto.LoadBitmap",
            async cancellationToken =>
            {
                _ = cancellationToken;
                var loadStart = DateTime.UtcNow;
                PhotoOverlayDiagnostics.Log(
                    "load-start",
                    $"req={requestId} path={IOPath.GetFileName(path)}");
                var bitmap = await LoadBitmapAsync(path);
                var decodeElapsedMs = (DateTime.UtcNow - loadStart).TotalMilliseconds;
                PhotoOverlayDiagnostics.Log(
                    "load-decoded",
                    $"req={requestId} path={IOPath.GetFileName(path)} elapsedMs={decodeElapsedMs:F0} bitmap={(bitmap != null ? $"{bitmap.PixelWidth}x{bitmap.PixelHeight}" : "null")}");
                if (requestId != Volatile.Read(ref _photoLoadRequestId))
                {
                    PhotoOverlayDiagnostics.Log(
                        "load-stale",
                        $"req={requestId} path={IOPath.GetFileName(path)} currentReq={Volatile.Read(ref _photoLoadRequestId)} elapsedMs={(DateTime.UtcNow - loadStart).TotalMilliseconds:F0}");
                    return;
                }

                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                {
                    PhotoOverlayDiagnostics.Log(
                        "load-discarded",
                        $"req={requestId} path={IOPath.GetFileName(path)} dispatcherShuttingDown=true elapsedMs={(DateTime.UtcNow - loadStart).TotalMilliseconds:F0}");
                    return;
                }

                void ApplyLoadedBitmapOnUi()
                {
                    if (requestId != Volatile.Read(ref _photoLoadRequestId))
                    {
                        PhotoOverlayDiagnostics.Log(
                            "apply-stale",
                            $"req={requestId} path={IOPath.GetFileName(path)} currentReq={Volatile.Read(ref _photoLoadRequestId)}");
                        return;
                    }

                    if (bitmap != null)
                    {
                        _cachedBitmapPath = path;
                        _cachedBitmap = bitmap;
                    }
                    PhotoOverlayDiagnostics.Log(
                        "apply-ui",
                        $"req={requestId} path={IOPath.GetFileName(path)} bitmap={(bitmap != null ? $"{bitmap.PixelWidth}x{bitmap.PixelHeight}" : "null")}");
                    ApplyLoadedBitmap(
                        requestId,
                        bitmap,
                        studentName,
                        durationSeconds,
                        hideWhenFailed: true,
                        ensureVisibleOnApply: deferShowUntilBitmapReady);
                }

                if (Dispatcher.CheckAccess())
                {
                    PhotoOverlayDiagnostics.Log(
                        "apply-dispatch",
                        $"req={requestId} path={IOPath.GetFileName(path)} inline=true queueMs=0 priority=Normal");
                    ApplyLoadedBitmapOnUi();
                    return;
                }

                var scheduled = false;
                var dispatchQueuedUtc = DateTime.UtcNow;
                try
                {
                    await Dispatcher.InvokeAsync(ApplyLoadedBitmapOnUi, DispatcherPriority.Normal);
                    scheduled = true;
                    PhotoOverlayDiagnostics.Log(
                        "apply-dispatch",
                        $"req={requestId} path={IOPath.GetFileName(path)} inline=false queueMs={(DateTime.UtcNow - dispatchQueuedUtc).TotalMilliseconds:F0} priority=Normal");
                }
                catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[PhotoOverlayWindow] async apply dispatch failed: {ex.GetType().Name} - {ex.Message}");
                    PhotoOverlayDiagnostics.Log(
                        "apply-dispatch-failed",
                        $"req={requestId} path={IOPath.GetFileName(path)} ex={ex.GetType().Name} msg={ex.Message}");
                }
                if (!scheduled && Dispatcher.CheckAccess())
                {
                    ApplyLoadedBitmapOnUi();
                }
            },
            CancellationToken.None,
            ex =>
            {
                System.Diagnostics.Debug.WriteLine($"[PhotoOverlayWindow] Failed to load bitmap async: {path}. Error: {ex.Message}");
                PhotoOverlayDiagnostics.Log(
                    "load-failed",
                    $"req={requestId} path={IOPath.GetFileName(path)} ex={ex.GetType().Name} msg={ex.Message}");
            });
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

        // 定位姓名徽标：紧贴照片上边沿中央，保留少量安全边距。
        if (NameBadge.Visibility == Visibility.Visible)
        {
            NameText.MaxWidth = Math.Max(220, photoWidth - 72);
            NameBadge.MaxWidth = NameText.MaxWidth + 56;
            NameBadge.Measure(new WpfSize(NameBadge.MaxWidth, double.PositiveInfinity));
            var badgeWidth = NameBadge.DesiredSize.Width;
            var badgeLeft = Math.Max(16, photoLeft + (photoWidth - badgeWidth) / 2);
            var badgeTop = Math.Max(12, photoTop + 10);
            Canvas.SetLeft(NameBadge, badgeLeft);
            Canvas.SetTop(NameBadge, badgeTop);
        }

        // 定位关闭按钮：主入口固定在右下角
        var buttonMargin = 30.0;
        var buttonSize = 56.0;
        
        Canvas.SetLeft(CloseButtonLeft, photoLeft + buttonMargin);
        Canvas.SetTop(CloseButtonLeft, photoTop + photoHeight - buttonMargin - buttonSize);

        Canvas.SetLeft(CloseButtonRight, photoLeft + photoWidth - buttonMargin - buttonSize);
        Canvas.SetTop(CloseButtonRight, photoTop + photoHeight - buttonMargin - buttonSize);
        Canvas.SetLeft(CloseButtonCenter, photoLeft + (photoWidth - buttonSize) / 2);
        Canvas.SetTop(CloseButtonCenter, photoTop + photoHeight - buttonMargin - buttonSize);

        // 关闭提示文案位于底部居中，避免遮挡主体内容。
        CloseHintText.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        var hintLeft = Math.Max(16, (windowWidth - CloseHintText.DesiredSize.Width) / 2);
        var hintTop = Math.Max(16, photoTop + photoHeight - 26);
        Canvas.SetLeft(CloseHintText, hintLeft);
        Canvas.SetTop(CloseHintText, hintTop);
    }

    public void CloseOverlay()
    {
        Interlocked.Increment(ref _photoLoadRequestId);
        _autoCloseTimer.Stop();
        _autoCloseDueUtc = default;
        PhotoOverlayDiagnostics.Log(
            "close",
            $"path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} studentId={_currentStudentId ?? string.Empty}");
        ClearPhotoCache(enterHideGuardState: true);
        LoadingMask.Visibility = Visibility.Visible;
        Opacity = 0.0;
        Hide();
    }

    private void OnAutoCloseTick(object? sender, EventArgs e)
    {
        var overdueMs = _autoCloseDueUtc == default
            ? 0
            : Math.Max(0, (DateTime.UtcNow - _autoCloseDueUtc).TotalMilliseconds);
        PhotoOverlayDiagnostics.Log(
            "auto-close",
            $"path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} studentId={_currentStudentId ?? string.Empty} overdueMs={overdueMs:F0}");
        CloseOverlay();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        CloseOverlay();
    }

    private void ClearPhotoCache(bool enterHideGuardState)
    {
        PhotoImage.Source = null;
        PhotoImage.Visibility = Visibility.Collapsed;
        UpdateStudentName(null, visible: false);
        LoadingMask.Visibility = enterHideGuardState ? Visibility.Visible : Visibility.Collapsed;
        LoadingMask.Background = _defaultLoadingMaskBrush;
        Opacity = enterHideGuardState ? 0.0 : 1.0;
        var studentId = _currentStudentId;
        _currentStudentId = null;
        _currentPhotoPath = null;
        if (!string.IsNullOrWhiteSpace(studentId))
        {
            SafeActionExecutionExecutor.TryExecute(
                () => PhotoClosed?.Invoke(studentId),
                ex => Debug.WriteLine($"[PhotoOverlayWindow] photo closed callback failed: {ex.Message}"));
        }
    }

    private bool TryGetCachedBitmap(string path, out BitmapSource bitmap)
    {
        if (_cachedBitmap != null
            && !string.IsNullOrWhiteSpace(_cachedBitmapPath)
            && string.Equals(_cachedBitmapPath, path, StringComparison.OrdinalIgnoreCase))
        {
            bitmap = _cachedBitmap;
            return true;
        }

        bitmap = null!;
        return false;
    }

    private void ApplyLoadedBitmap(
        int requestId,
        BitmapSource? bitmap,
        string? studentName,
        int durationSeconds,
        bool hideWhenFailed,
        bool ensureVisibleOnApply)
    {
        if (requestId != Volatile.Read(ref _photoLoadRequestId))
        {
            PhotoOverlayDiagnostics.Log(
                "apply-stale",
                $"req={requestId} currentReq={Volatile.Read(ref _photoLoadRequestId)} bitmap={(bitmap != null ? $"{bitmap.PixelWidth}x{bitmap.PixelHeight}" : "null")}");
            return;
        }
        if (bitmap == null)
        {
            _autoCloseTimer.Stop();
            LoadingMask.Visibility = Visibility.Collapsed;
            LoadingMask.Background = _defaultLoadingMaskBrush;
            PhotoOverlayDiagnostics.Log(
                "apply-null",
                $"req={requestId} path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} hideWhenFailed={hideWhenFailed}");
            if (hideWhenFailed)
            {
                Hide();
            }
            return;
        }

        PhotoImage.Source = bitmap;
        PhotoImage.Visibility = Visibility.Visible;
        if (ensureVisibleOnApply || !IsVisible)
        {
            EnsureOverlayVisible();
            PhotoOverlayDiagnostics.Log(
                "show-visible",
                $"req={requestId} path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} visible={IsVisible} topmost={Topmost} state={WindowState} via=apply");
        }
        Opacity = 1.0;
        PhotoOverlayDiagnostics.Log(
            "apply-success",
            $"req={requestId} path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} bitmap={bitmap.PixelWidth}x{bitmap.PixelHeight} duration={durationSeconds}");
        DeferHideLoadingMaskAfterRender(requestId);

        void ApplyOverlayLayoutAfterPhotoLoad()
        {
            if (requestId != Volatile.Read(ref _photoLoadRequestId))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(studentName))
            {
                UpdateStudentName(studentName, visible: true);
            }
            UpdateOverlayPositions();
        }

        // First-pass sync apply keeps name/layout responsive; async pass below remains for
        // post-render stabilization when size metadata arrives one tick later.
        ApplyOverlayLayoutAfterPhotoLoad();

        var scheduled = false;
        if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
        {
            try
            {
                _ = Dispatcher.BeginInvoke(
                    new Action(ApplyOverlayLayoutAfterPhotoLoad),
                    DispatcherPriority.Background);
                scheduled = true;
            }
            catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine(
                    $"[PhotoOverlayWindow] deferred layout dispatch failed: {ex.GetType().Name} - {ex.Message}");
            }
        }
        if (!scheduled)
        {
            if (Dispatcher.CheckAccess())
            {
                ApplyOverlayLayoutAfterPhotoLoad();
            }
            else
            {
                Debug.WriteLine("[PhotoOverlayWindow] deferred layout dispatch failed");
            }
        }

        UpdateAutoCloseTimer(durationSeconds);
    }

    private static Task<BitmapImage?> LoadBitmapAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Task.FromResult<BitmapImage?>(null);
        }

        return Task.Run(() => LoadBitmap(path));
    }

    private bool IsShowingSamePhoto(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && string.Equals(_currentPhotoPath, path, StringComparison.OrdinalIgnoreCase)
            && PhotoImage.Source != null
            && PhotoImage.Visibility == Visibility.Visible
            && LoadingMask.Visibility != Visibility.Visible;
    }

    private void UpdateAutoCloseTimer(int durationSeconds)
    {
        if (durationSeconds > 0)
        {
            _autoCloseTimer.Interval = TimeSpan.FromSeconds(durationSeconds);
            _autoCloseDueUtc = DateTime.UtcNow.AddSeconds(durationSeconds);
            _autoCloseTimer.Start();
            PhotoOverlayDiagnostics.Log(
                "auto-close-start",
                $"req={_photoLoadRequestId} path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} duration={durationSeconds}");
            return;
        }

        _autoCloseTimer.Stop();
        _autoCloseDueUtc = default;
        PhotoOverlayDiagnostics.Log(
            "auto-close-stop",
            $"req={_photoLoadRequestId} path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} duration={durationSeconds}");
    }

    private void EnsureOverlayVisible()
    {
        ApplyWindowedBounds();
        if (!IsVisible)
        {
            Show();
        }

        WindowTopmostExecutor.ApplyNoActivate(this, enabled: true, enforceZOrder: true);
    }

    private void UpdateStudentName(string? studentName, bool visible)
    {
        NameText.Text = studentName?.Trim() ?? string.Empty;
        NameBadge.Visibility = visible && !string.IsNullOrWhiteSpace(NameText.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
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
            var decodePixelWidth = ResolveDecodePixelWidth();
            if (decodePixelWidth > 0)
            {
                bitmap.DecodePixelWidth = decodePixelWidth;
            }
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

    private static int ResolveDecodePixelWidth()
    {
        var maxEdge = Math.Max(
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        if (double.IsNaN(maxEdge) || double.IsInfinity(maxEdge) || maxEdge <= 0)
        {
            return 0;
        }

        var scaled = (int)Math.Ceiling(maxEdge * 0.9);
        return Math.Max(1024, Math.Min(2048, scaled));
    }

    private void ApplyWindowedBounds()
    {
        var screenWidth = SystemParameters.VirtualScreenWidth;
        var screenHeight = SystemParameters.VirtualScreenHeight;
        if (screenWidth <= 0 || screenHeight <= 0)
        {
            return;
        }

        var targetWidth = Math.Clamp(screenWidth * 0.72, 900, 1680);
        var targetHeight = Math.Clamp(screenHeight * 0.72, 580, 980);
        Width = Math.Min(targetWidth, screenWidth - 64);
        Height = Math.Min(targetHeight, screenHeight - 64);
        Left = SystemParameters.VirtualScreenLeft + (screenWidth - Width) / 2;
        Top = SystemParameters.VirtualScreenTop + (screenHeight - Height) / 2;
    }

    private void DeferHideLoadingMaskAfterRender(int requestId)
    {
        void HideMask()
        {
            if (requestId != Volatile.Read(ref _photoLoadRequestId))
            {
                return;
            }

            LoadingMask.Background = _defaultLoadingMaskBrush;
            LoadingMask.Visibility = Visibility.Collapsed;
            PhotoOverlayDiagnostics.Log(
                "mask-hide",
                $"req={requestId} path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} mode=deferred-render");
        }

        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            HideMask();
            return;
        }

        var scheduled = false;
        try
        {
            _ = Dispatcher.BeginInvoke(new Action(HideMask), DispatcherPriority.Render);
            scheduled = true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PhotoOverlayWindow] defer-hide-mask dispatch failed: {ex.GetType().Name} - {ex.Message}");
        }

        if (!scheduled && Dispatcher.CheckAccess())
        {
            HideMask();
        }
    }

    private static SolidColorBrush CreateOpaqueFrameGuardBrush()
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x09, 0x10, 0x16));
        brush.Freeze();
        return brush;
    }

}
