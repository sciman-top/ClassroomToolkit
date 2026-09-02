using System.IO;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

[SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action-based photo close callback is an existing UI contract covered by callback-safety contract tests.")]
public partial class PhotoOverlayWindow : Window
{
    private readonly DispatcherTimer _autoCloseTimer;
    private readonly System.Windows.Media.Brush _defaultLoadingMaskBrush;
    private DateTime _autoCloseDueUtc;
    private string? _currentStudentId;
    private string? _currentPhotoPath;
    private IntPtr _hwnd;
    private int _photoLoadRequestId;
    private CancellationTokenSource? _photoLoadCts;
    private string? _cachedBitmapPath;
    private BitmapSource? _cachedBitmap;
    private Window? _zOrderAnchor;
    private static readonly SolidColorBrush OpaqueFrameGuardBrush = CreateOpaqueFrameGuardBrush();

    public event Action<string?>? PhotoClosed;

    public bool IsDisplayActive => IsVisible
        && Opacity > 0.0
        && PhotoImage.Source != null
        && PhotoImage.Visibility == Visibility.Visible;

    public PhotoOverlayWindow()
    {
        InitializeComponent();
        ShowActivated = false;
        Focusable = false;
        IsHitTestVisible = false;
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
        // WPF's SystemParameters values are DIPs and may describe the work area
        // on a per-monitor-DPI process.  Reapply the native monitor bounds after
        // the HWND exists so the overlay also covers the taskbar area.
        ApplyWindowedBounds();
        SetInputPassthrough(enabled: !IsHitTestVisible || Opacity <= 0.0);
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        Interlocked.Increment(ref _photoLoadRequestId);
        CancelPendingPhotoLoad();
        _autoCloseTimer.Stop();
        _autoCloseTimer.Tick -= OnAutoCloseTick;
        SourceInitialized -= OnOverlaySourceInitialized;
        Closed -= OnOverlayClosed;
        ClearPhotoCache(enterHideGuardState: false);
    }

    public void ShowPhoto(string path, string studentName, string studentId, int durationSeconds, Window? zOrderAnchor)
    {
        _zOrderAnchor = zOrderAnchor;
        var requestId = Interlocked.Increment(ref _photoLoadRequestId);
        CancelPendingPhotoLoad();
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
        IsHitTestVisible = false;

        // 先清空上一张图并进入遮挡态，避免窗口可见时先闪出旧图。
        PhotoImage.Source = null;
        PhotoImage.Visibility = Visibility.Collapsed;
        LoadingMask.Visibility = Visibility.Visible;
        LoadingMask.Background = OpaqueFrameGuardBrush;
        // Force immediate visual state commit while this window is still on-screen
        // so the previous frame is less likely to flash for a single composition frame.
        UpdateLayout();
        var becameVisibleForPrewarm = EnsureOverlayVisible();
        if (becameVisibleForPrewarm)
        {
            DeferInitialZOrderRetouch(requestId);
        }

        if (!deferShowUntilBitmapReady)
        {
            _ = EnsureOverlayVisible();
            PhotoOverlayDiagnostics.Log(
                "show-visible",
                $"req={requestId} path={IOPath.GetFileName(path)} visible={IsVisible} topmost={Topmost} state={WindowState}");
        }
        else
        {
            ApplyWindowedBounds();
            PhotoOverlayDiagnostics.Log(
                "show-prewarm",
                $"req={requestId} path={IOPath.GetFileName(path)} visible={IsVisible} prewarmed={becameVisibleForPrewarm}");
        }
        // 窗口复显后再次施加透明保护，避免复显首帧复用旧合成帧。
        Opacity = 0.0;
        IsHitTestVisible = false;
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

        _photoLoadCts = new CancellationTokenSource();
        var loadToken = _photoLoadCts.Token;
        _ = SafeTaskRunner.Run(
            "PhotoOverlayWindow.ShowPhoto.LoadBitmap",
            async cancellationToken =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var loadStart = DateTime.UtcNow;
                PhotoOverlayDiagnostics.Log(
                    "load-start",
                    $"req={requestId} path={IOPath.GetFileName(path)}");
                var bitmap = await LoadBitmapAsync(path);
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
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
#pragma warning disable CA2016 // Intentionally avoid token forwarding to keep UI fallback contract stable.
                    await Dispatcher.InvokeAsync(ApplyLoadedBitmapOnUi, DispatcherPriority.Normal);
#pragma warning restore CA2016
                    scheduled = true;
                    PhotoOverlayDiagnostics.Log(
                        "apply-dispatch",
                        $"req={requestId} path={IOPath.GetFileName(path)} inline=false queueMs={(DateTime.UtcNow - dispatchQueuedUtc).TotalMilliseconds:F0} priority=Normal");
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
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
            loadToken,
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

        // Let Uniform calculate one shared scale from the full-screen viewport.
        PhotoImage.Width = e.NewSize.Width;
        PhotoImage.Height = e.NewSize.Height;
        Canvas.SetLeft(PhotoImage, 0);
        Canvas.SetTop(PhotoImage, 0);
        Canvas.SetLeft(CloseLeftButton, 16);
        Canvas.SetTop(CloseLeftButton, Math.Max(0, e.NewSize.Height - CloseLeftButton.Height - 16));
        Canvas.SetLeft(CloseRightButton, Math.Max(0, e.NewSize.Width - CloseRightButton.Width - 16));
        Canvas.SetTop(CloseRightButton, Math.Max(0, e.NewSize.Height - CloseRightButton.Height - 16));

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

    }

    public void CloseOverlay()
    {
        if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }

        Interlocked.Increment(ref _photoLoadRequestId);
        CancelPendingPhotoLoad();
        _autoCloseTimer.Stop();
        _autoCloseDueUtc = default;
        PhotoOverlayDiagnostics.Log(
            "close",
            $"path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} studentId={_currentStudentId ?? string.Empty}");
        ClearPhotoCache(enterHideGuardState: true);
        LoadingMask.Visibility = Visibility.Visible;
        EnterInactivePassthroughState();
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

    private void OnPhotoImageMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        CloseOverlay();
    }

    private void OnCloseButtonClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
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
                EnterInactivePassthroughState();
            }
            return;
        }

        PhotoImage.Source = bitmap;
        PhotoImage.Visibility = Visibility.Visible;
        var becameVisible = false;
        if (ensureVisibleOnApply || !IsVisible)
        {
            becameVisible = EnsureOverlayVisible();
            PhotoOverlayDiagnostics.Log(
                "show-visible",
                $"req={requestId} path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} visible={IsVisible} topmost={Topmost} state={WindowState} via=apply");
            if (becameVisible)
            {
                DeferRevealAfterInitialZOrderRetouch(requestId);
            }
        }
        if (!becameVisible)
        {
            RevealOverlay();
        }
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

    private bool EnsureOverlayVisible()
    {
        ApplyWindowedBounds();
        var becameVisible = false;
        if (!IsVisible)
        {
            WindowTopmostExecutor.PrepareNoActivateBehind(this, _zOrderAnchor);
            Show();
            becameVisible = true;
            // SourceInitialized normally applies this already, but the second
            // pass closes the race where WPF restores the previous work-area
            // placement while the window is being shown.
            ApplyWindowedBounds();
        }

        WindowTopmostExecutor.ApplyNoActivateBehind(this, _zOrderAnchor);
        if (becameVisible)
        {
            SafeActionExecutionExecutor.TryExecute(
                () =>
                {
                    if (System.Windows.Application.Current?.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.RequestImmediateFloatingZOrderRetouch();
                    }
                },
                ex => Debug.WriteLine($"[PhotoOverlayWindow] immediate z-order retouch failed: {ex.Message}"));
        }

        return becameVisible;
    }

    private void DeferRevealAfterInitialZOrderRetouch(int requestId)
    {
        DeferZOrderRetouch(requestId, reveal: true);
    }

    private void DeferInitialZOrderRetouch(int requestId)
    {
        DeferZOrderRetouch(requestId, reveal: false);
    }

    private void DeferZOrderRetouch(int requestId, bool reveal)
    {
        try
        {
            _ = Dispatcher.InvokeAsync(
                () =>
                {
                    if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
                    {
                        return;
                    }

                    if (requestId != Volatile.Read(ref _photoLoadRequestId) || !IsVisible)
                    {
                        return;
                    }

                    WindowTopmostExecutor.ApplyNoActivateBehind(this, _zOrderAnchor);
                    if (System.Windows.Application.Current?.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.RequestImmediateFloatingZOrderRetouch();
                    }

                    if (reveal)
                    {
                        RevealOverlay();
                    }

                    PhotoOverlayDiagnostics.Log(
                        reveal ? "show-reveal" : "show-prewarm-retouch",
                        $"req={requestId} path={IOPath.GetFileName(_currentPhotoPath ?? string.Empty)} mode=deferred-zorder-retouch");
                },
                DispatcherPriority.Render);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[PhotoOverlayWindow] deferred z-order retouch dispatch failed: {ex.GetType().Name} - {ex.Message}");
            if (reveal)
            {
                RevealOverlay();
            }
        }
    }

    private void RevealOverlay()
    {
        SetInputPassthrough(enabled: false);
        IsHitTestVisible = true;
        Opacity = 1.0;
    }

    private void EnterInactivePassthroughState()
    {
        Opacity = 0.0;
        IsHitTestVisible = false;
        SetInputPassthrough(enabled: true);
    }

    private void SetInputPassthrough(bool enabled)
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        _ = WindowStyleExecutor.TryUpdateExtendedStyleBits(
            _hwnd,
            setMask: enabled ? WindowStyleBitMasks.WsExTransparent : 0,
            clearMask: enabled ? 0 : WindowStyleBitMasks.WsExTransparent,
            out _);
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
        var screenBounds = ResolveTargetScreenBounds();
        if (screenBounds.Width <= 0 || screenBounds.Height <= 0)
        {
            return;
        }

        // Keep WPF's logical layout in sync with the physical HWND placement.
        // The native path below covers the taskbar in device pixels, but WPF
        // still measures RootCanvas from Width/Height.  Leaving those values
        // at the previous work-area size produces a one-sided black strip.
        var dipBounds = ResolveScreenBoundsInDip(screenBounds);
        Left = dipBounds.Left;
        Top = dipBounds.Top;
        Width = dipBounds.Width;
        Height = dipBounds.Height;

        var hwnd = ResolveOverlayWindowHandle();
        if (hwnd != IntPtr.Zero
            && WindowPlacementExecutor.TryApplyBoundsNoActivateNoZOrder(
                hwnd,
                screenBounds.X,
                screenBounds.Y,
                screenBounds.Width,
                screenBounds.Height,
                showWindow: IsVisible))
        {
            PhotoOverlayDiagnostics.Log(
                "bounds",
                $"mode=native screen={screenBounds.X},{screenBounds.Y},{screenBounds.Width}x{screenBounds.Height} dip={dipBounds.Left:0.##},{dipBounds.Top:0.##},{dipBounds.Width:0.##}x{dipBounds.Height:0.##}");
            return;
        }

        // Native positioning is unavailable only before SourceInitialized or
        // when Windows rejects a transient placement call.  Keep the WPF
        // fallback DPI-correct; SourceInitialized/EnsureOverlayVisible will
        // retry the physical-pixel path once the HWND is ready.
        PhotoOverlayDiagnostics.Log(
            "bounds",
            $"mode=dip screen={screenBounds.X},{screenBounds.Y},{screenBounds.Width}x{screenBounds.Height} dip={dipBounds.Left:0.##},{dipBounds.Top:0.##},{dipBounds.Width:0.##}x{dipBounds.Height:0.##}");
    }

    private IntPtr ResolveOverlayWindowHandle()
    {
        if (_hwnd != IntPtr.Zero)
        {
            return _hwnd;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            _hwnd = handle;
        }

        return handle;
    }

    private System.Drawing.Rectangle ResolveTargetScreenBounds()
    {
        var anchorHandle = _zOrderAnchor == null
            ? IntPtr.Zero
            : new WindowInteropHelper(_zOrderAnchor).Handle;
        var handle = anchorHandle != IntPtr.Zero ? anchorHandle : _hwnd;
        if (handle != IntPtr.Zero)
        {
            try
            {
                return System.Windows.Forms.Screen.FromHandle(handle).Bounds;
            }
            catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine(
                    $"[PhotoOverlayWindow] monitor bounds lookup failed: {ex.GetType().Name} - {ex.Message}");
            }
        }

        return System.Windows.Forms.SystemInformation.VirtualScreen;
    }

    private System.Windows.Rect ResolveScreenBoundsInDip(System.Drawing.Rectangle screenBounds)
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var matrix = source.CompositionTarget.TransformFromDevice;
            var topLeft = matrix.Transform(new System.Windows.Point(screenBounds.Left, screenBounds.Top));
            var bottomRight = matrix.Transform(new System.Windows.Point(screenBounds.Right, screenBounds.Bottom));
            return new System.Windows.Rect(topLeft, bottomRight);
        }

        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var scaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        var scaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;
        return new System.Windows.Rect(
            screenBounds.Left / scaleX,
            screenBounds.Top / scaleY,
            screenBounds.Width / scaleX,
            screenBounds.Height / scaleY);
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

    private void CancelPendingPhotoLoad()
    {
        var cts = Interlocked.Exchange(ref _photoLoadCts, null);
        if (cts == null)
        {
            return;
        }

        try
        {
            cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore races during shutdown.
        }
        finally
        {
            cts.Dispose();
        }
    }

    private static Task<BitmapImage?> LoadBitmapAsync(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return Task.FromResult<BitmapImage?>(null);
        }

        // 解码必须离开调用方线程：假异步（Task.FromResult(LoadBitmap(path))）一旦被
        // UI 线程直接 await 就会整卡一帧大图解码。
        return Task.Run(() => LoadBitmap(path));
    }

}
