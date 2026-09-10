using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private const int WmDisplayChange = 0x007E;
    private const int WmDpiChanged = 0x02E0;
    private void OnOverlayLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
        EnsureRasterSurface();
    }

    private void OnPresentationFocusMonitorTick(object? sender, EventArgs e)
    {
        if (ShouldIgnoreLifecycleTick())
        {
            _presentationFocusMonitor.Stop();
            return;
        }

        MonitorPresentationFocus();
    }

    private void OnInkMonitorTick(object? sender, EventArgs e)
    {
        if (ShouldIgnoreLifecycleTick())
        {
            _inkMonitor.Stop();
            return;
        }

        _refreshOrchestrator.RequestRefresh("poll");
    }

    private void OnInkSidecarAutoSaveTimerTick(object? sender, EventArgs e)
    {
        _inkSidecarAutoSaveTimer?.Stop();
        if (ShouldIgnoreLifecycleTick())
        {
            return;
        }

        if (IsInkOperationActive())
        {
            _inkDiagnostics?.OnAutoSaveDeferred("timer-active-operation");
            ScheduleSidecarAutoSave();
            return;
        }
        if (!TryCaptureSidecarPersistSnapshot(requireDirty: true, out var snapshot) || snapshot == null)
        {
            return;
        }
        QueueSidecarAutoSave(snapshot);
    }

    private void OnOverlayVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            WindowPlacementHelper.EnsureVisible(this);
        }

        // Update the native authorization set synchronously.  The async hook
        // state refresh remains the lifecycle owner, but hidden HWNDs must not
        // stay authorized during its scheduling window.
        RefreshPresentationInputOwnership();
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
        UpdatePresentationFocusMonitor();
    }

    private void OnOverlaySourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        _lastAppliedInputPassthroughEnabled = null;
        _lastAppliedFocusBlocked = null;
        if (_hwnd != IntPtr.Zero && HwndSource.FromHwnd(_hwnd) is { } source)
        {
            source.AddHook(OnOverlayHwndHook);
        }
        UpdateInputPassthrough();
        UpdateFocusAcceptance();
    }

    private IntPtr OnOverlayHwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmDisplayChange || msg == WmDpiChanged)
        {
            // WM_DPICHANGED 的 lParam 只在当前消息回调期间有效；先复制物理像素
            // suggested RECT，再切回 UI 队列应用，避免异步回调读到失效指针。
            DpiSuggestedBounds suggestedBounds = default;
            var hasSuggestedBounds = msg == WmDpiChanged
                && DpiSuggestedBoundsInterop.TryCopy(lParam, out suggestedBounds);
            Action recovery = hasSuggestedBounds
                ? () => RecoverAfterDisplaySettingsChange(suggestedBounds)
                : () => RecoverAfterDisplaySettingsChange();

            // 投影仪热插拔、分辨率或每显示器 DPI 变化后覆盖层几何和栅格
            // surface 都可能过期，统一按当前模式延迟重铺。
            var scheduled = TryBeginInvoke(recovery, DispatcherPriority.Background);
            if (!scheduled && Dispatcher.CheckAccess())
            {
                recovery();
            }
        }
        return IntPtr.Zero;
    }

    private void RecoverAfterDisplaySettingsChange(
        DpiSuggestedBounds? suggestedBounds = null)
    {
        if (ShouldIgnoreLifecycleTick() || !IsVisible)
        {
            return;
        }
        if (IsPhotoFullscreenActive)
        {
            // A true fullscreen photo surface owns the complete target monitor.
            // WM_DPICHANGED's suggested RECT preserves a normal window's logical
            // size, but it can leave a fullscreen overlay with uncovered edges.
            ApplyPhotoWindowBounds(fullscreen: true);
            EnsureRasterSurface();
            return;
        }
        if (_photoModeActive)
        {
            // The non-fullscreen photo mode is the only windowed overlay state;
            // use the OS suggestion when available and retain the current mode
            // when display topology changes without a DPI suggestion.
            var positioned = suggestedBounds.HasValue
                && TryApplyDpiSuggestedBounds(suggestedBounds.Value);
            if (!positioned)
            {
                ApplyPhotoWindowBounds(fullscreen: false);
            }
        }
        else
        {
            // Board/presentation overlay remains monitor-bound even when WPF's
            // DPI suggestion describes a smaller logical window.
            RecoverOverlayFullscreenBounds();
        }
        EnsureRasterSurface();
    }

    private bool TryApplyDpiSuggestedBounds(DpiSuggestedBounds suggestedBounds)
    {
        var width = (long)suggestedBounds.Right - suggestedBounds.Left;
        var height = (long)suggestedBounds.Bottom - suggestedBounds.Top;
        if (width <= 0 || width > int.MaxValue || height <= 0 || height > int.MaxValue)
        {
            return false;
        }

        var hwnd = ResolveOverlayWindowHandle();
        if (hwnd == IntPtr.Zero)
        {
            return false;
        }

        NormalizeOverlayWindowState(shouldNormalize: true);
        return WindowPlacementExecutor.TryApplyBoundsNoActivateNoZOrder(
            hwnd,
            suggestedBounds.Left,
            suggestedBounds.Top,
            (int)width,
            (int)height,
            showWindow: true);
    }

    private void OnOverlayDeactivated(object? sender, EventArgs e)
    {
        HandlePointerCaptureLoss("overlay-deactivated");
    }

    private void OnOverlayClosed(object? sender, EventArgs e)
    {
        Interlocked.Exchange(ref _overlayClosed, 1);
        _overlayLifecycleCancellation.Cancel();
        HandlePointerCaptureLoss("overlay-closed");
        if (_hwnd != IntPtr.Zero)
        {
            try
            {
                if (HwndSource.FromHwnd(_hwnd) is { } closingSource)
                {
                    closingSource.RemoveHook(OnOverlayHwndHook);
                }
            }
            catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine($"[PaintOverlay] displaychange hook removal failed: {ex.GetType().Name} - {ex.Message}");
            }
        }
        Closed -= OnOverlayClosed;
        KeyDown -= OnKeyDown;
        Loaded -= OnOverlayLoaded;
        IsVisibleChanged -= OnOverlayVisibleChanged;
        SourceInitialized -= OnOverlaySourceInitialized;
        Deactivated -= OnOverlayDeactivated;
        MouseWheel -= OnMouseWheel;
        SizeChanged -= OnWindowSizeChanged;
        StateChanged -= OnWindowStateChanged;
        OverlayRoot.MouseLeftButtonDown -= OnMouseDown;
        OverlayRoot.MouseMove -= OnMouseMove;
        OverlayRoot.MouseLeftButtonUp -= OnMouseUp;
        OverlayRoot.MouseRightButtonDown -= OnRightButtonDown;
        OverlayRoot.MouseRightButtonUp -= OnRightButtonUp;
        OverlayRoot.MouseLeave -= OnOverlayMouseLeave;
        OverlayRoot.LostMouseCapture -= OnOverlayLostMouseCapture;
        OverlayRoot.ManipulationStarting -= OnManipulationStarting;
        OverlayRoot.ManipulationInertiaStarting -= OnManipulationInertiaStarting;
        OverlayRoot.ManipulationDelta -= OnManipulationDelta;
        OverlayRoot.ManipulationCompleted -= OnManipulationCompleted;
        OverlayRoot.TouchDown -= OnTouchDown;
        OverlayRoot.TouchMove -= OnTouchMove;
        OverlayRoot.TouchUp -= OnTouchUp;
        OverlayRoot.LostTouchCapture -= OnOverlayLostTouchCapture;
        if (_photoPanInertiaRenderingAttached)
        {
            CompositionTarget.Rendering -= OnPhotoPanInertiaRendering;
            _photoPanInertiaRenderingAttached = false;
        }
        StopPhotoZoomRendering();
        OverlayRoot.StylusDown -= OnStylusDown;
        OverlayRoot.StylusMove -= OnStylusMove;
        OverlayRoot.StylusUp -= OnStylusUp;
        OverlayRoot.LostStylusCapture -= OnOverlayLostStylusCapture;
        _photoActiveTouchIds.Clear();
        _photoTouchPanDeviceId = null;
        SaveCurrentPageIfNeeded();
        DisposeRasterHistory();
        _inkHistory.Clear();
        _globalInkHistory.Clear();
        _activeInkOperationHistory = null;
        _photoTransformSaveTimer?.Stop();
        _photoTransformSaveTimer?.Tick -= OnPhotoTransformSaveTimerTick;
        _photoUnifiedTransformSaveTimer?.Stop();
        _photoUnifiedTransformSaveTimer?.Tick -= OnPhotoUnifiedTransformSaveTimerTick;
        _presentationFocusMonitor.Tick -= OnPresentationFocusMonitorTick;
        _photoRenderQualityRestoreTimer.Tick -= OnPhotoRenderQualityRestoreTimerTick;
        _photoRenderQualityRestoreTimer.Stop();
        _inkMonitor.Tick -= OnInkMonitorTick;
        _inkSidecarAutoSaveTimer?.Tick -= OnInkSidecarAutoSaveTimerTick;
        _inkSidecarAutoSaveTimer?.Stop();
        _inkSidecarAutoSaveGate.NextGeneration();
        _inkWal.Dispose();
        _wpsNavHookStateGate.NextGeneration();
        StopWpsNavHook();
        if (_wpsNavHook != null && _wpsNavHook.Available)
        {
            _wpsNavHook.NavigationRequestCaptured -= OnWpsNavigationRequestCaptured;
            _wpsNavHook.Dispose();
        }
        _wpsNavHookStateGate.Dispose();
        _inkSidecarAutoSaveGate.Dispose();
        _presentationFocusMonitor.Stop();
        _inkMonitor.Stop();
        ClosePdfDocument();
        _overlayLifecycleCancellation.Dispose();
    }

    private bool ShouldIgnoreLifecycleTick()
    {
        return Volatile.Read(ref _overlayClosed) != 0 || _overlayLifecycleCancellation.IsCancellationRequested;
    }
}
