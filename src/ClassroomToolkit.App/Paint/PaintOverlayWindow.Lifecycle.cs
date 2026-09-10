using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;

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
            // 投影仪热插拔、分辨率或每显示器 DPI 变化后覆盖层几何和栅格
            // surface 都可能过期，统一按当前模式延迟重铺。
            var scheduled = TryBeginInvoke(RecoverAfterDisplaySettingsChange, DispatcherPriority.Background);
            if (!scheduled && Dispatcher.CheckAccess())
            {
                RecoverAfterDisplaySettingsChange();
            }
        }
        return IntPtr.Zero;
    }

    private void RecoverAfterDisplaySettingsChange()
    {
        if (ShouldIgnoreLifecycleTick() || !IsVisible)
        {
            return;
        }
        if (IsPhotoFullscreenActive)
        {
            ApplyPhotoWindowBounds(fullscreen: true);
            EnsureRasterSurface();
            return;
        }
        RecoverOverlayFullscreenBounds();
        EnsureRasterSurface();
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
