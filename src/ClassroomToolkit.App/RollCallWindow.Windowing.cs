using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.Domain.Utilities;
using ClassroomToolkit.Interop;
using WpfSize = System.Windows.Size;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public partial class RollCallWindow
{
    public void SyncTopmost(bool enabled)
    {
        Topmost = enabled;
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var insertAfter = enabled ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost;
        NativeMethods.SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }
    
    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_dataLoaded)
        {
            return;
        }
        if (e.ChangedButton == MouseButton.Left)
        {
            if (e.OriginalSource is DependencyObject source && IsInteractiveElement(source))
            {
                return;
            }
            DragMove();
        }
    }

    private void OnBackgroundDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_dataLoaded)
        {
            return;
        }
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (e.OriginalSource is DependencyObject source)
        {
            if (IsInteractiveElement(source))
            {
                return;
            }
            // 姓名卡片和计时器卡片需要响应点击/展示，不应用作窗口拖动区域
            if (IsDescendantOf(source, RollNameCard) || IsDescendantOf(source, TimerCard))
            {
                return;
            }
        }
        try
        {
            DragMove();
            e.Handled = true;
        }
        catch
        {
            // Ignore drag exceptions.
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            HidePhotoOverlay();
            UpdateGroupNameDisplay();
            PersistSettings();
            _viewModel.SaveState();
            _rollStateSaveTimer.Stop();
            _rollStateDirty = false;
            return;
        }
        _timer.Stop();
        _stopwatch.Stop();
        _rollStateSaveTimer.Stop();
        _windowBoundsSaveTimer.Stop();
        _rollStateDirty = false;
        _remoteHookStartGate.NextGeneration();
        StopKeyboardHook();
        ClosePhotoOverlay();
        if (_groupOverlay != null)
        {
            try { _groupOverlay.Close(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RollCallWindow: close group overlay failed: {ex.GetType().Name} - {ex.Message}");
            }
            _groupOverlay = null;
        }

        PersistSettings();
        _viewModel.SaveState();
    }

    private static bool IsInteractiveElement(DependencyObject source)
    {
        var current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase
                || current is System.Windows.Controls.ComboBox
                || current is System.Windows.Controls.Primitives.TextBoxBase
                || current is System.Windows.Controls.Primitives.Selector
                || current is System.Windows.Controls.Primitives.MenuBase
                || current is System.Windows.Controls.Primitives.ScrollBar
                || current is System.Windows.Controls.Primitives.Thumb
                || current is System.Windows.Controls.Primitives.ToggleButton)
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private static bool IsDescendantOf(DependencyObject source, DependencyObject? ancestor)
    {
        if (ancestor == null)
        {
            return false;
        }
        var current = source;
        while (current != null)
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }

    private void OnWindowMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hovering = true;
        UpdateWindowTransparency();
    }

    private void OnWindowMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        _hovering = false;
        UpdateWindowTransparency();
    }

    private void UpdateWindowTransparency()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var allowTransparent = !_hovering && PaintModeManager.Instance.ShouldAllowTransparency(isToolbar: false);
        UpdateHoverTimer(allowTransparent);
        var exStyle = NativeMethods.GetWindowLong(_hwnd, NativeMethods.GwlExstyle);
        if (allowTransparent)
        {
            exStyle |= NativeMethods.WsExTransparent;
        }
        else
        {
            exStyle &= ~NativeMethods.WsExTransparent;
        }
        NativeMethods.SetWindowLong(_hwnd, NativeMethods.GwlExstyle, exStyle);
    }

    private void UpdateHoverTimer(bool transparentEnabled)
    {
        if (!transparentEnabled)
        {
            _hoverCheckTimer.Stop();
            return;
        }
        if (!_hoverCheckTimer.IsEnabled)
        {
            _hoverCheckTimer.Start();
        }
    }

    private void UpdateHoverState()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        if (!NativeMethods.GetCursorPos(out var point))
        {
            return;
        }
        NativeMethods.NativeRect rect;
        if (!NativeMethods.GetWindowRect(_hwnd, out rect))
        {
            return;
        }
        var inside = point.X >= rect.Left && point.X <= rect.Right && point.Y >= rect.Top && point.Y <= rect.Bottom;
        if (inside == _hovering)
        {
            return;
        }
        _hovering = inside;
        UpdateWindowTransparency();
    }

    private void OnWindowVisibilityChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        UpdateGroupNameDisplay();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        UpdateGroupNameDisplay();
    }

    private void ShowGroupOverlay()
    {
        UpdateGroupNameDisplay();
    }

    private void UpdateGroupNameDisplay()
    {
        if (!_viewModel.RemoteGroupSwitchEnabled || !_viewModel.IsRollCallMode)
        {
            // 未启用分组切换功能或不在点名模式，不显示组名
            if (_groupOverlay != null)
            {
                _groupOverlay.HideGroup();
            }
            return;
        }

        var shouldShowPersistent = WindowState == WindowState.Minimized || !IsVisible;
        
        if (shouldShowPersistent)
        {
            // 点名窗口隐藏时：持久显示组名
            if (_groupOverlay == null)
            {
                _groupOverlay = new RollCallGroupOverlayWindow();
                _groupOverlay.Closed += (s, e) => _groupOverlay = null;
            }
            _groupOverlay.ShowGroup(_viewModel.CurrentGroup, persistent: true);
        }
        else
        {
            // 点名窗口显示时：绝不显示组名
            if (_groupOverlay != null)
            {
                _groupOverlay.HideGroup();
            }
        }
    }

    private void ApplyWindowBounds(AppSettings settings)
    {
        if (settings.RollCallWindowWidth > 0)
        {
            Width = settings.RollCallWindowWidth;
        }
        if (settings.RollCallWindowHeight > 0)
        {
            Height = settings.RollCallWindowHeight;
        }
        if (settings.RollCallWindowX != AppSettings.UnsetPosition
            && settings.RollCallWindowY != AppSettings.UnsetPosition)
        {
            Left = settings.RollCallWindowX;
            Top = settings.RollCallWindowY;
            WindowPlacementHelper.EnsureVisible(this);
        }
        else
        {
            WindowPlacementHelper.CenterOnVirtualScreen(this);
        }
    }

    private void CaptureWindowBounds()
    {
        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        _settings.RollCallWindowWidth = (int)Math.Round(width);
        _settings.RollCallWindowHeight = (int)Math.Round(height);
        _settings.RollCallWindowX = (int)Math.Round(Left);
        _settings.RollCallWindowY = (int)Math.Round(Top);
    }

    private void ScheduleWindowBoundsSave()
    {
        if (!IsLoaded)
        {
            return;
        }
        _windowBoundsDirty = true;
        if (_windowBoundsSaveTimer.IsEnabled)
        {
            _windowBoundsSaveTimer.Stop();
        }
        _windowBoundsSaveTimer.Start();
    }

    private void SaveWindowBoundsIfNeeded()
    {
        _windowBoundsSaveTimer.Stop();
        if (!_windowBoundsDirty)
        {
            return;
        }
        _windowBoundsDirty = false;
        CaptureWindowBounds();
        SaveSettingsSafe();
    }

    private void UpdateMinWindowSize()
    {
        if (!IsLoaded)
        {
            return;
        }
        Dispatcher.BeginInvoke(() =>
        {
            var titleSize = MeasureElement(TitleBarRoot);
            var bottomSize = MeasureElement(BottomBarRoot);
            var groupButtonsSize = MeasureElement(GroupButtonsControl);

            var minWidth = titleSize.Width;
            if (_viewModel.IsRollCallMode)
            {
                minWidth = Math.Max(minWidth, groupButtonsSize.Width + GetBottomBarChromeWidth());
            }

            var minHeight = titleSize.Height + bottomSize.Height;

            MinWidth = Math.Max(240, Math.Ceiling(minWidth));
            MinHeight = Math.Max(240, Math.Ceiling(minHeight));
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private double GetBottomBarChromeWidth()
    {
        if (BottomBarRoot == null)
        {
            return 0;
        }
        var padding = BottomBarRoot.Padding;
        var border = BottomBarRoot.BorderThickness;
        var margin = BottomBarRoot.Margin;
        return padding.Left + padding.Right + border.Left + border.Right + margin.Left + margin.Right;
    }

    private static WpfSize MeasureElement(FrameworkElement? element)
    {
        if (element == null)
        {
            return new WpfSize(0, 0);
        }
        element.Measure(new WpfSize(double.PositiveInfinity, double.PositiveInfinity));
        var desired = element.DesiredSize;
        var margin = element.Margin;
        return new WpfSize(
            desired.Width + margin.Left + margin.Right,
            desired.Height + margin.Top + margin.Bottom);
    }
}
