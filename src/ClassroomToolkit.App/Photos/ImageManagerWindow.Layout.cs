using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
    private void RemoveMinimizeButton()
    {
        var hwnd = _hwnd != IntPtr.Zero ? _hwnd : new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (!WindowStyleExecutor.TryUpdateStyleBits(
                hwnd,
                WindowStyleBitMasks.GwlStyle,
                setMask: 0,
                clearMask: WindowStyleBitMasks.WsMinimizeBox,
                out var style))
        {
            return;
        }
        if ((style & WindowStyleBitMasks.WsMinimizeBox) != 0) return;
        WindowPlacementExecutor.TryRefreshFrame(hwnd);
    }

    private void OnMainColumnSplitterDragDelta(object sender, DragDeltaEventArgs e)
    {
        UpdatePreferredLeftLayoutFromCurrent();
    }

    private void OnMainColumnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isClosing || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }
        void ApplyDeferredSplitterUpdate()
        {
            UpdatePreferredLeftLayoutFromCurrent();
            SafeActionExecutionExecutor.TryExecute(
                () => LeftPanelLayoutChanged?.Invoke(_preferredLeftRatio, _preferredLeftPanelWidth),
                ex => Debug.WriteLine($"ImageManager: layout callback failed: {ex.Message}"));
        }

        var scheduled = false;
        try
        {
            Dispatcher.BeginInvoke(
                new Action(ApplyDeferredSplitterUpdate),
                DispatcherPriority.Background);
            scheduled = true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: deferred splitter update dispatch failed: {ex.GetType().Name} - {ex.Message}");
        }
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ApplyDeferredSplitterUpdate();
        }
    }

    private void ApplyAdaptiveLayout()
    {
        if (_layoutApplying || !IsLoaded || RootGrid == null)
        {
            return;
        }

        var totalWidth = RootGrid.ActualWidth;
        if (totalWidth <= 0)
        {
            return;
        }

        _layoutApplying = true;
        try
        {
            var splitterWidth = Math.Max(0, SplitterColumn.ActualWidth > 0 ? SplitterColumn.ActualWidth : 6);
            var available = Math.Max(1, totalWidth - splitterWidth);

            var effectiveRatio = _preferredLeftRatio;
            if (ActualWidth > 0 && ActualWidth < NarrowWindowThreshold)
            {
                effectiveRatio = Math.Min(effectiveRatio, 0.27);
            }

            var minRight = RightColumn.MinWidth > 0 ? RightColumn.MinWidth : 560.0;
            var desiredLeft = _preferredLeftPanelWidth > 0
                ? _preferredLeftPanelWidth
                : available * effectiveRatio;
            var maxLeft = double.IsInfinity(LeftColumn.MaxWidth) ? available - minRight : LeftColumn.MaxWidth;
            maxLeft = Math.Max(LeftColumn.MinWidth, Math.Min(maxLeft, available - minRight));
            var boundedLeft = Math.Clamp(desiredLeft, LeftColumn.MinWidth, maxLeft);

            if (available - boundedLeft < minRight)
            {
                boundedLeft = Math.Max(LeftColumn.MinWidth, available - minRight);
            }

            boundedLeft = Math.Clamp(boundedLeft, LeftColumn.MinWidth, maxLeft);
            var boundedRight = Math.Max(minRight, available - boundedLeft);

            LeftColumn.Width = new GridLength(Math.Round(boundedLeft), GridUnitType.Pixel);
            RightColumn.Width = new GridLength(Math.Round(boundedRight), GridUnitType.Pixel);
            _preferredLeftRatio = boundedLeft / available;
            _preferredLeftPanelWidth = (int)Math.Round(boundedLeft);
        }
        finally
        {
            _layoutApplying = false;
        }
    }

    private double CalculateCurrentLeftRatio()
    {
        var splitterWidth = Math.Max(0, SplitterColumn.ActualWidth > 0 ? SplitterColumn.ActualWidth : 6);
        var total = Math.Max(1, RootGrid.ActualWidth - splitterWidth);
        var left = LeftColumn.ActualWidth > 0
            ? LeftColumn.ActualWidth
            : (LeftColumn.Width.IsAbsolute ? LeftColumn.Width.Value : LeftColumn.MinWidth);
        return left / total;
    }

    private void UpdatePreferredLeftLayoutFromCurrent()
    {
        if (!IsLoaded || RootGrid == null)
        {
            return;
        }

        _preferredLeftRatio = NormalizeLeftRatio(CalculateCurrentLeftRatio(), _preferredLeftRatio);
        _preferredLeftPanelWidth = LeftColumn.ActualWidth > 0
            ? (int)Math.Round(LeftColumn.ActualWidth)
            : (int)Math.Round(LeftColumn.Width.IsAbsolute ? LeftColumn.Width.Value : 0);
    }

    private static double NormalizeLeftRatio(double ratio, double fallback)
    {
        if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio <= 0)
        {
            return fallback;
        }
        return Math.Clamp(ratio, MinLeftRatio, MaxLeftRatio);
    }

    private static double NormalizeThumbnailSize(double size, double fallback)
    {
        if (double.IsNaN(size) || double.IsInfinity(size) || size <= 0)
        {
            return fallback;
        }
        return Math.Clamp(size, MinThumbnailSize, MaxThumbnailSize);
    }

    private void EnterInitialMaximizedState()
    {
        if (!IsLoaded)
        {
            return;
        }

        WindowState = WindowState.Maximized;
    }

    private void TrackRestoredWindowSize()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (width > 0)
        {
            _restoredWindowWidth = width;
        }
        if (height > 0)
        {
            _restoredWindowHeight = height;
        }
    }

    private void OnRestoreWindowClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RestoreWindowToManagedBounds();
            return;
        }

        WindowState = WindowState.Maximized;
    }

    private void OnCloseWindowClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            if (WindowState == WindowState.Maximized)
            {
                RestoreWindowToManagedBounds();
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
            e.Handled = true;
            return;
        }

        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _ = this.SafeDragMove();
    }

    private void RestoreWindowToManagedBounds()
    {
        if (WindowState == WindowState.Normal)
        {
            return;
        }

        WindowState = WindowState.Normal;
        if (_isClosing || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }
        void ApplyRestoredBounds()
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            var plan = ImageManagerRestoreBoundsPolicy.Resolve(
                restoredWidth: _restoredWindowWidth,
                restoredHeight: _restoredWindowHeight,
                defaultWidth: DefaultWindowWidth,
                defaultHeight: DefaultWindowHeight,
                minWidth: MinWidth,
                minHeight: MinHeight,
                workArea: SystemParameters.WorkArea);

            Width = plan.Width;
            Height = plan.Height;
            if (!double.IsNaN(plan.Left))
            {
                Left = plan.Left;
            }
            if (!double.IsNaN(plan.Top))
            {
                Top = plan.Top;
            }

            TrackRestoredWindowSize();
            ApplyAdaptiveLayout();
        }

        var scheduled = false;
        try
        {
            Dispatcher.BeginInvoke(
                new Action(ApplyRestoredBounds),
                DispatcherPriority.Loaded);
            scheduled = true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: restore bounds dispatch failed: {ex.GetType().Name} - {ex.Message}");
        }
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ApplyRestoredBounds();
        }
    }

    private void UpdateWindowStateToggleButton()
    {
        if (WindowStateToggleButton == null)
        {
            return;
        }

        WindowStateToggleButton.Content = WindowState == WindowState.Maximized ? "还原" : "最大化";
    }
}
