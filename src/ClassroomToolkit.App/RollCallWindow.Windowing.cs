using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.RollCall;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.Domain.Utilities;
using WpfSize = System.Windows.Size;
using ClassroomToolkit.App.Settings;

namespace ClassroomToolkit.App;

public partial class RollCallWindow
{
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

            _ = TryDragMoveSafe();
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
        if (TryDragMoveSafe())
        {
            e.Handled = true;
        }
    }

    private bool TryDragMoveSafe()
    {
        return this.SafeDragMove(ex => System.Diagnostics.Debug.WriteLine(
            RollCallWindowDiagnosticsPolicy.FormatDragMoveFailureMessage(
                ex.GetType().Name,
                ex.Message)));
    }

    /// <summary>
    /// 统一的隐藏点名窗口操作：保存状态、隐藏照片叠加、更新组名显示。
    /// 用于关闭按钮和启动器"隐藏点名"按钮的归一处理。
    /// </summary>
    public void HideRollCall()
    {
        ExecuteRollCallSafe("hide-rollcall-window", Hide);
        HidePhotoOverlay();
        UpdateGroupNameDisplay();
        PersistSettings();
        _viewModel.SaveState();
        _rollStateSaveTimer.Stop();
        _rollStateDirty = false;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        HideRollCall();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            HideRollCall();
            return;
        }
        if (_closingCleanupStarted)
        {
            return;
        }
        _closingCleanupStarted = true;
        _lifecycleCancellation.Cancel();
        _timer.Stop();
        _stopwatch.Stop();
        _rollStateSaveTimer.Stop();
        _windowBoundsSaveTimer.Stop();
        _hoverCheckTimer.Stop();
        _rollStateDirty = false;
        Loaded -= OnLoaded;
        Closing -= OnClosing;
        PreviewKeyDown -= OnPreviewKeyDown;
        SourceInitialized -= OnSourceInitialized;
        MouseEnter -= OnWindowMouseEnter;
        MouseLeave -= OnWindowMouseLeave;
        IsVisibleChanged -= OnWindowVisibilityChanged;
        StateChanged -= OnWindowStateChanged;
        _timer.Tick -= OnTimerTick;
        _rollStateSaveTimer.Tick -= OnRollStateSaveTick;
        _windowBoundsSaveTimer.Tick -= OnWindowBoundsSaveTick;
        _hoverCheckTimer.Tick -= OnHoverCheckTimerTick;
        SizeChanged -= OnWindowSizeChanged;
        LocationChanged -= OnWindowLocationChanged;
        _viewModel.GroupButtons.CollectionChanged -= OnGroupButtonsCollectionChanged;
        _viewModel.TimerCompleted -= OnTimerCompleted;
        _viewModel.ReminderTriggered -= OnReminderTriggered;
        _viewModel.DataLoadFailed -= OnDataLoadFailed;
        _viewModel.DataSaveFailed -= OnDataSaveFailed;
        PaintModeManager.Instance.PaintModeChanged -= OnPaintModeChanged;
        PaintModeManager.Instance.IsDrawingChanged -= OnDrawingStateChanged;
        _speechService.SpeechUnavailable -= OnSpeechUnavailable;
        _remoteHookStartGate.NextGeneration();
        StopKeyboardHook();
        _remoteHookStartGate.Dispose();
        ClosePhotoOverlay();
        _photoResolver?.Dispose();
        _photoResolver = null;
        if (_groupOverlay != null)
        {
            _groupOverlay.Closed -= OnGroupOverlayClosed;
            ExecuteRollCallSafe("close-group-overlay-window", _groupOverlay.Close);
            _groupOverlay = null;
        }

        PersistSettings();
        _viewModel.SaveState();
        _viewModel.Dispose();
        _lifecycleCancellation.Dispose();
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
        var transparencyDecision = RollCallTransparencyPolicy.ResolveTransparency(
            hovering: _hovering,
            paintAllowsTransparency: PaintModeManager.Instance.ShouldAllowTransparency(isToolbar: false));
        var allowTransparent = transparencyDecision.TransparentEnabled;
        UpdateHoverTimer(allowTransparent);
        var styleApplyDecision = RollCallTransparencyPolicy.ResolveStyleApply(
            transparentEnabled: allowTransparent,
            lastTransparentEnabled: _lastTransparentStyleEnabled);
        if (!styleApplyDecision.ShouldApplyStyle)
        {
            return;
        }

        var (setMask, clearMask) = RollCallTransparencyPolicy.ResolveStyleMasks(allowTransparent);
        if (WindowStyleExecutor.TryUpdateExtendedStyleBits(
                _hwnd,
                setMask,
                clearMask,
                out _))
        {
            _lastTransparentStyleEnabled = allowTransparent;
        }
    }

    private void UpdateHoverTimer(bool transparentEnabled)
    {
        var hoverTimerDecision = RollCallTransparencyPolicy.ResolveHoverTimer(
            transparentEnabled: transparentEnabled,
            hoverTimerEnabled: _hoverCheckTimer.IsEnabled);
        if (hoverTimerDecision.ShouldStop)
        {
            _hoverCheckTimer.Stop();
            return;
        }

        if (hoverTimerDecision.ShouldStart)
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
        if (!WindowCursorHitTestExecutor.TryIsCursorInsideWindow(_hwnd, out var inside))
        {
            return;
        }
        if (inside == _hovering)
        {
            return;
        }
        _hovering = inside;
        UpdateWindowTransparency();
    }

    private void OnWindowVisibilityChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            WindowPlacementHelper.EnsureVisible(this);
            UpdateWindowTransparency();
        }

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
                ExecuteGroupOverlaySafe("hide-group", _groupOverlay.HideGroup);
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
                _groupOverlay.Closed += OnGroupOverlayClosed;
            }
            ExecuteGroupOverlaySafe(
                "show-group",
                () => _groupOverlay.ShowGroup(_viewModel.CurrentGroup, persistent: true));
        }
        else
        {
            // 点名窗口显示时：绝不显示组名
            if (_groupOverlay != null)
            {
                ExecuteGroupOverlaySafe("hide-group", _groupOverlay.HideGroup);
            }
        }
    }

    private void OnGroupOverlayClosed(object? sender, EventArgs e)
    {
        if (sender is RollCallGroupOverlayWindow overlay)
        {
            overlay.Closed -= OnGroupOverlayClosed;
        }

        if (ReferenceEquals(_groupOverlay, sender))
        {
            _groupOverlay = null;
        }
    }

    private void ExecuteGroupOverlaySafe(string operation, Action action)
    {
        SafeActionExecutionExecutor.TryExecute(
            action,
            ex => System.Diagnostics.Debug.WriteLine(
                RollCallWindowDiagnosticsPolicy.FormatGroupOverlayFailureMessage(
                    operation,
                    ex.GetType().Name,
                    ex.Message)));
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
        if (!RollCallRemoteHookDispatchPolicy.CanDispatch(
                Dispatcher.HasShutdownStarted,
                Dispatcher.HasShutdownFinished))
        {
            return;
        }
        _ = Dispatcher.InvokeAsync(() =>
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

