using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void SetPhotoWindowMode(bool fullscreen)
    {
        var wasFullscreen = _photoFullscreen;
        _photoFullscreen = fullscreen;
        var fullscreenChanged = wasFullscreen != _photoFullscreen;
        if (_photoModeActive && wasFullscreen && !fullscreen)
        {
            SaveAndClearInkSurface();
        }
        PhotoControlLayer.Visibility = _photoModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;
        PhotoWindowFrame.BorderThickness = _photoModeActive && !_photoFullscreen
            ? new Thickness(1)
            : new Thickness(0);
        if (_photoModeActive)
        {
            PhotoWindowFrame.Background = ResolvePhotoWindowBackgroundBrush();
        }
        else
        {
            PhotoWindowFrame.Background = MediaBrushes.Transparent;
        }
        if (_photoModeActive)
        {
            ResizeMode = _photoFullscreen ? ResizeMode.NoResize : ResizeMode.CanResize;
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.CaptionHeight = _photoFullscreen ? 0 : 28;
                chrome.ResizeBorderThickness = _photoFullscreen ? new Thickness(0) : new Thickness(6);
            }
            ApplyPhotoWindowBounds(_photoFullscreen);
            if (_photoFullscreen)
            {
                // Reassert topmost with force only when overlay is not topmost yet.
                var enforceTopmost = OverlayTopmostEnforcePolicy.ResolveForPhotoFullscreen(Topmost);
                EnsureOverlayTopmost(enforceZOrder: enforceTopmost);
                SchedulePhotoFullscreenBoundsEnforcement();
            }
        }
        else
        {
            ResizeMode = ResizeMode.NoResize;
            Interlocked.Increment(ref _photoFullscreenBoundsToken);
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.CaptionHeight = 28;
                chrome.ResizeBorderThickness = new Thickness(6);
            }
            // Avoid leaving the overlay in Maximized(work-area) semantics.
            // A stale work-area maximize state can leak into the next photo fullscreen transition.
            RecoverOverlayFullscreenBounds();
        }
        ShowInTaskbar = _photoModeActive;
        UpdateOverlayHitTestVisibility();

        // 全屏模式下禁用标题栏的鼠标交互，防止拖动窗口
        if (PhotoTitleBar != null)
        {
            PhotoTitleBar.IsHitTestVisible = !fullscreen;
        }

        UpdateInputPassthrough();
        if (PhotoWindowModeZOrderRetouchPolicy.ShouldRequest(_photoModeActive, fullscreenChanged))
        {
            var forceEnforce = PhotoWindowModeZOrderRetouchPolicy.ShouldForceEnforce(_photoFullscreen);
            SafeActionExecutionExecutor.TryExecute(
                () => FloatingZOrderRequested?.Invoke(new FloatingZOrderRequest(forceEnforce)),
                ex => Debug.WriteLine($"[FloatingZOrderRequested] photo-window-mode callback failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    private MediaBrush ResolvePhotoWindowBackgroundBrush()
    {
        if (_boardColor.A == 0)
        {
            return MediaBrushes.White;
        }

        var color = System.Windows.Media.Color.FromArgb(255, _boardColor.R, _boardColor.G, _boardColor.B);
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private void ApplyIdentityPhotoTransform()
    {
        _photoScale.ScaleX = 1.0;
        _photoScale.ScaleY = 1.0;
        _photoTranslate.X = 0;
        _photoTranslate.Y = 0;
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: true);
    }

    private void ApplyPhotoWindowBounds(bool fullscreen)
    {
        NormalizeOverlayWindowState(shouldNormalize: true);
        if (fullscreen)
        {
            var hwnd = ResolveOverlayWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                // Use device pixels to guarantee true monitor coverage (including taskbar area).
                var pxRect = GetCurrentMonitorRect(useWorkArea: false);
                var positioned = WindowPlacementExecutor.TryApplyBoundsNoActivateNoZOrder(
                    hwnd,
                    (int)Math.Round(pxRect.Left, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Top, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Width, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Height, MidpointRounding.AwayFromZero),
                    showWindow: true);
                if (positioned)
                {
                    return;
                }
            }
        }

        var rect = GetCurrentMonitorRectInDip(useWorkArea: !fullscreen);
        Left = rect.Left;
        Top = rect.Top;
        Width = rect.Width;
        Height = rect.Height;
    }

    private void UpdatePhotoContentTransforms(bool enabled)
    {
        var applyPhotoTransform = PhotoContentTransformPolicy.ShouldApplyPhotoTransform(
            enabledRequested: enabled,
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            transformAvailable: _photoContentTransform != null);
        RasterImage.RenderTransform = applyPhotoTransform
            ? _photoContentTransform!
            : _photoInkPanCompensation;
        if (!applyPhotoTransform)
        {
            ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: !IsPhotoInkModeActive());
            SyncPhotoInteractiveRefreshAnchor();
        }
        UpdatePhotoInkClip();
    }

    private void RefreshPhotoBackgroundVisibility()
    {
        PhotoBackground.Visibility = PhotoBackgroundVisibilityPolicy.Resolve(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            hasBackgroundSource: PhotoBackground.Source != null);
        UpdatePhotoInkClip();
    }

    private void UpdatePhotoInkClip()
    {
        Rect rasterClipBounds = Rect.Empty;
        Rect previewClipBounds = Rect.Empty;
        if (!_photoUnboundedInkCanvasEnabled && PhotoBackground.Source is BitmapSource bitmap)
        {
            var usePhotoTransform = ReferenceEquals(RasterImage.RenderTransform, _photoContentTransform);
            _ = TryBuildImageScreenRect(bitmap, _photoContentTransform, out var currentPageScreenRect);
            rasterClipBounds = PhotoInkCurrentPageClipPolicy.ResolveBounds(
                photoInkModeActive: IsPhotoInkModeActive(),
                crossPageDisplayActive: IsCrossPageDisplayActive(),
                photoFullscreenActive: IsPhotoFullscreenActive,
                usePhotoTransform: usePhotoTransform,
                currentPageScreenRect: currentPageScreenRect,
                pageWidthDip: GetBitmapDisplayWidthInDip(bitmap),
                pageHeightDip: GetBitmapDisplayHeightInDip(bitmap));
            previewClipBounds = PhotoInkPreviewClipPolicy.ResolveBounds(
                photoInkModeActive: IsPhotoInkModeActive(),
                crossPageDisplayActive: IsCrossPageDisplayActive(),
                photoFullscreenActive: IsPhotoFullscreenActive,
                usePhotoTransform: usePhotoTransform,
                currentPageScreenRect: currentPageScreenRect,
                pageWidthDip: GetBitmapDisplayWidthInDip(bitmap),
                pageHeightDip: GetBitmapDisplayHeightInDip(bitmap));
        }

        if (rasterClipBounds.IsEmpty)
        {
            RasterImage.Clip = null;
        }
        else if (RasterImage.Clip is RectangleGeometry rectangleClip)
        {
            rectangleClip.Rect = rasterClipBounds;
        }
        else
        {
            RasterImage.Clip = new RectangleGeometry(rasterClipBounds);
        }

        if (previewClipBounds.IsEmpty)
        {
            _visualHost.Clip = null;
        }
        else if (_visualHost.Clip is RectangleGeometry previewClip)
        {
            previewClip.Rect = previewClipBounds;
        }
        else
        {
            _visualHost.Clip = new RectangleGeometry(previewClipBounds);
        }
    }

    private IntPtr ResolveOverlayWindowHandle()
    {
        if (WindowHandleValidationInteropAdapter.IsValid(_hwnd))
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

    private void SchedulePhotoFullscreenBoundsEnforcement()
    {
        if (!IsPhotoFullscreenActive)
        {
            return;
        }
        var token = Interlocked.Increment(ref _photoFullscreenBoundsToken);
        var lifecycleToken = _overlayLifecycleCancellation.Token;
        _ = SafeTaskRunner.Run(
            "PaintOverlayWindow.SchedulePhotoFullscreenBoundsEnforcement",
            async cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var delays = new[] { 30, 120, 280 };
                foreach (var delayMs in delays)
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    var scheduled = TryBeginInvoke(() =>
                    {
                        if (token != _photoFullscreenBoundsToken || !IsPhotoFullscreenActive)
                        {
                            return;
                        }
                        ApplyPhotoWindowBounds(fullscreen: true);
                    }, DispatcherPriority.Render);
                    if (!scheduled && Dispatcher.CheckAccess())
                    {
                        if (token != _photoFullscreenBoundsToken || !IsPhotoFullscreenActive)
                        {
                            return;
                        }
                        ApplyPhotoWindowBounds(fullscreen: true);
                    }
                    else if (!scheduled)
                    {
                        Debug.WriteLine("[PhotoBounds] fullscreen-enforcement dispatch unavailable.");
                    }
                }
            },
            lifecycleToken,
            onError: ex => Debug.WriteLine(
                $"[PhotoBounds] fullscreen-enforcement failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private void ApplyFullscreenBounds()
    {
        var rect = GetCurrentMonitorRect();
        Left = rect.Left;
        Top = rect.Top;
        Width = rect.Width;
        Height = rect.Height;
    }

    private Rect GetCurrentMonitorRect(bool useWorkArea = false)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
        var r = useWorkArea ? screen.WorkingArea : screen.Bounds;
        return new Rect(r.X, r.Y, r.Width, r.Height);
    }
    
    private Rect GetCurrentMonitorRectInDip(bool useWorkArea = false)
    {
        var screenRect = GetCurrentMonitorRect(useWorkArea);
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var matrix = source.CompositionTarget.TransformFromDevice;
            var topLeft = matrix.Transform(new WpfPoint(screenRect.Left, screenRect.Top));
            var bottomRight = matrix.Transform(new WpfPoint(screenRect.Right, screenRect.Bottom));
            return new Rect(topLeft, bottomRight);
        }
        return screenRect;
    }
}
