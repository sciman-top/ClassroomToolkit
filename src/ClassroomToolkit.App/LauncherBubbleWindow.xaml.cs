using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class LauncherBubbleWindow : Window
{
    private enum EdgeSide
    {
        Left,
        Right,
        Top,
        Bottom
    }

    private bool _dragging;
    private bool _moved;
    private System.Windows.Point _dragOffset;
    private System.Windows.Point _dragStartPosition;
    private System.Drawing.Rectangle _dragScreenBounds;
    private System.Drawing.Rectangle _dragWorkingArea;
    private bool _hasDragScreenArea;
    private IntPtr _hwnd;
    private IDisposable? _dragScope;
    private int? _activeTouchId;
    
    // 拖动阈值：移动超过此距离才算拖动，否则算点击
    private const double DragThreshold = 5.0;
    private const double DragThresholdSquared = DragThreshold * DragThreshold;

    // Windows API 常量
    // Windows API 常量
    // private const int GwlExstyle = -20;
    // private const int WsExNoActivate = 0x08000000;
    // private const int WsExToolWindow = 0x00000080;

    public LauncherBubbleWindow()
    {
        InitializeComponent();
        Cursor = System.Windows.Input.Cursors.Hand;
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
        TouchDown += OnTouchDown;
        TouchMove += OnTouchMove;
        TouchUp += OnTouchUp;
        LostTouchCapture += OnLostTouchCapture;
        Loaded += OnWindowLoaded;
        IsVisibleChanged += OnWindowVisibleChanged;
        Closed += OnWindowClosed;
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // 设置窗口样式，避免获取焦点
        SetWindowNoActivate();
    }

    private void OnWindowVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            WindowPlacementHelper.EnsureVisible(this);
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        EndDragScope();
        MouseLeftButtonDown -= OnMouseDown;
        MouseMove -= OnMouseMove;
        MouseLeftButtonUp -= OnMouseUp;
        TouchDown -= OnTouchDown;
        TouchMove -= OnTouchMove;
        TouchUp -= OnTouchUp;
        LostTouchCapture -= OnLostTouchCapture;
        Loaded -= OnWindowLoaded;
        IsVisibleChanged -= OnWindowVisibleChanged;
        Closed -= OnWindowClosed;
    }

    private void SetWindowNoActivate()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }

        _ = WindowStyleExecutor.TryUpdateStyleBits(
            _hwnd,
            WindowStyleBitMasks.GwlExStyle,
            setMask: WindowStyleBitMasks.WsExNoActivate | WindowStyleBitMasks.WsExToolWindow,
            clearMask: 0,
            out _);
    }

    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action-based events are the existing launcher bubble contract.")]
    public event Action? RestoreRequested;
    [SuppressMessage("Design", "CA1003:Use generic event handler instances", Justification = "Action-based events are the existing launcher bubble contract.")]
    public event Action<System.Windows.Point>? PositionChanged;

    public void PlaceNear(System.Windows.Point target)
    {
        var screenPoint = new System.Drawing.Point((int)target.X, (int)target.Y);
        var screen = System.Windows.Forms.Screen.FromPoint(screenPoint);
        var area = screen.WorkingArea;
        var margin = 6;
        var center = new System.Windows.Point(target.X, target.Y);
        center.X = Math.Max(area.Left, Math.Min(center.X, area.Right));
        center.Y = Math.Max(area.Top, Math.Min(center.Y, area.Bottom));

        var leftDistance = Math.Abs(center.X - area.Left);
        var rightDistance = Math.Abs(area.Right - center.X);
        var topDistance = Math.Abs(center.Y - area.Top);
        var bottomDistance = Math.Abs(area.Bottom - center.Y);
        var nearest = ResolveNearestEdge(leftDistance, rightDistance, topDistance, bottomDistance);

        double x;
        double y;
        if (nearest == EdgeSide.Left)
        {
            x = area.Left + margin;
            y = center.Y - Height / 2;
        }
        else if (nearest == EdgeSide.Right)
        {
            x = area.Right - Width - margin;
            y = center.Y - Height / 2;
        }
        else if (nearest == EdgeSide.Top)
        {
            x = center.X - Width / 2;
            y = area.Top + margin;
        }
        else
        {
            x = center.X - Width / 2;
            y = area.Bottom - Height - margin;
        }

        x = Math.Max(area.Left + margin, Math.Min(x, area.Right - Width - margin));
        y = Math.Max(area.Top + margin, Math.Min(y, area.Bottom - Height - margin));
        Left = x;
        Top = y;
        TryExecuteNonFatal(() => PositionChanged?.Invoke(new System.Windows.Point(Left, Top)));
    }

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        BeginDrag(e.GetPosition(this));
        
        // 捕获鼠标，防止拖动时失去鼠标事件
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        UpdateDrag(e.GetPosition(this));
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        
        // 释放鼠标捕获
        ReleaseMouseCapture();
        EndDrag(shouldRestoreWhenTap: true);
    }

    private void OnTouchDown(object? sender, TouchEventArgs e)
    {
        _activeTouchId = e.TouchDevice.Id;
        BeginDrag(e.GetTouchPoint(this).Position);
        CaptureTouch(e.TouchDevice);
        e.Handled = true;
    }

    private void OnTouchMove(object? sender, TouchEventArgs e)
    {
        if (_activeTouchId != e.TouchDevice.Id)
        {
            return;
        }

        UpdateDrag(e.GetTouchPoint(this).Position);
        e.Handled = true;
    }

    private void OnTouchUp(object? sender, TouchEventArgs e)
    {
        if (_activeTouchId != e.TouchDevice.Id)
        {
            return;
        }

        ReleaseTouchCapture(e.TouchDevice);
        EndDrag(shouldRestoreWhenTap: true);
        _activeTouchId = null;
        e.Handled = true;
    }

    private void OnLostTouchCapture(object? sender, TouchEventArgs e)
    {
        if (_activeTouchId != e.TouchDevice.Id)
        {
            return;
        }

        _dragging = false;
        _hasDragScreenArea = false;
        EndDragScope();
        _activeTouchId = null;
        e.Handled = true;
    }

    private void BeginDrag(System.Windows.Point position)
    {
        _dragging = true;
        _moved = false;
        _dragOffset = position;
        _dragStartPosition = new System.Windows.Point(Left, Top);
        TryUpdateDragScreenArea(PointToScreen(position));
        BeginDragScope();
    }

    private void UpdateDrag(System.Windows.Point position)
    {
        if (!_dragging)
        {
            return;
        }

        try
        {
            var screen = PointToScreen(position);
            var newX = screen.X - _dragOffset.X;
            var newY = screen.Y - _dragOffset.Y;
            
            // 使用缓存屏幕工作区边界，跨屏时再更新，减少高频查询开销。
            if (!_hasDragScreenArea || !_dragScreenBounds.Contains((int)screen.X, (int)screen.Y))
            {
                TryUpdateDragScreenArea(screen);
            }
            if (!_hasDragScreenArea)
            {
                return;
            }

            newX = Math.Max(_dragWorkingArea.Left, Math.Min(newX, _dragWorkingArea.Right - Width));
            newY = Math.Max(_dragWorkingArea.Top, Math.Min(newY, _dragWorkingArea.Bottom - Height));
            
            // 计算移动距离，超过阈值才算拖动
            var deltaX = newX - _dragStartPosition.X;
            var deltaY = newY - _dragStartPosition.Y;
            if (deltaX * deltaX + deltaY * deltaY > DragThresholdSquared)
            {
                _moved = true;
            }
            
            Left = newX;
            Top = newY;
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            // Ignore transient bubble drag/snap failures.
        }
    }

    private void EndDrag(bool shouldRestoreWhenTap)
    {
        _dragging = false;
        _hasDragScreenArea = false;
        EndDragScope();

        if (!_moved)
        {
            if (shouldRestoreWhenTap)
            {
                // 点击事件：恢复主窗口
                TryExecuteNonFatal(() => RestoreRequested?.Invoke());
            }
        }
        else
        {
            // 拖动结束：延迟吸附到边缘，避免卡顿
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                _moved = false;
                return;
            }

            void SnapBubbleToNearestEdge()
            {
                try
                {
                    var center = new System.Windows.Point(Left + Width / 2, Top + Height / 2);
                    PlaceNear(center);
                }
                catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
                {
                    // Ignore transient bubble drag/snap failures.
                }
            }

            var scheduled = false;
            TryExecuteNonFatal(() =>
            {
                _ = Dispatcher.InvokeAsync(
                    new Action(SnapBubbleToNearestEdge),
                    System.Windows.Threading.DispatcherPriority.Background);
                scheduled = true;
            });
            if (!scheduled && Dispatcher.CheckAccess())
            {
                SnapBubbleToNearestEdge();
            }
        }
        
        _moved = false;
    }

    private void BeginDragScope()
    {
        EndDragScope();
        _dragScope = WindowDragOperationState.Begin();
    }

    private void EndDragScope()
    {
        _dragScope?.Dispose();
        _dragScope = null;
    }

    private static void TryExecuteNonFatal(Action action)
    {
        try
        {
            action();
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            // Ignore transient bubble drag/snap failures.
        }
    }

    private void TryUpdateDragScreenArea(System.Windows.Point screenPosition)
    {
        TryExecuteNonFatal(() =>
        {
            var screen = System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point((int)screenPosition.X, (int)screenPosition.Y));
            _dragScreenBounds = screen.Bounds;
            _dragWorkingArea = screen.WorkingArea;
            _hasDragScreenArea = true;
        });
    }

    private static EdgeSide ResolveNearestEdge(double left, double right, double top, double bottom)
    {
        var min = left;
        var edge = EdgeSide.Left;
        if (right < min)
        {
            min = right;
            edge = EdgeSide.Right;
        }
        if (top < min)
        {
            min = top;
            edge = EdgeSide.Top;
        }
        if (bottom < min)
        {
            edge = EdgeSide.Bottom;
        }
        return edge;
    }

}
