using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

public partial class LauncherBubbleWindow : Window
{
    private bool _dragging;
    private bool _moved;
    private System.Windows.Point _dragOffset;
    private System.Windows.Point _dragStartPosition;
    private IntPtr _hwnd;
    
    // 拖动阈值：移动超过此距离才算拖动，否则算点击
    private const double DragThreshold = 5.0;

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
        Loaded += OnWindowLoaded;
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                WindowPlacementHelper.EnsureVisible(this);
            }
        };
    }

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        // 设置窗口样式，避免获取焦点
        SetWindowNoActivate();
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

    public event Action? RestoreRequested;
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

        var distances = new Dictionary<string, double>
        {
            ["left"] = Math.Abs(center.X - area.Left),
            ["right"] = Math.Abs(area.Right - center.X),
            ["top"] = Math.Abs(center.Y - area.Top),
            ["bottom"] = Math.Abs(area.Bottom - center.Y)
        };
        var nearest = distances.OrderBy(pair => pair.Value).First().Key;

        double x;
        double y;
        if (nearest == "left")
        {
            x = area.Left + margin;
            y = center.Y - Height / 2;
        }
        else if (nearest == "right")
        {
            x = area.Right - Width - margin;
            y = center.Y - Height / 2;
        }
        else if (nearest == "top")
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
        PositionChanged?.Invoke(new System.Windows.Point(Left, Top));
    }

    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        _dragging = true;
        _moved = false;
        _dragOffset = e.GetPosition(this);
        _dragStartPosition = new System.Windows.Point(Left, Top);
        
        // 捕获鼠标，防止拖动时失去鼠标事件
        CaptureMouse();
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        
        try
        {
            var screen = PointToScreen(e.GetPosition(this));
            var newX = screen.X - _dragOffset.X;
            var newY = screen.Y - _dragOffset.Y;
            
            // 使用当前屏幕的工作区域进行边界检查
            var screenPoint = new System.Drawing.Point((int)screen.X, (int)screen.Y);
            var currentScreen = System.Windows.Forms.Screen.FromPoint(screenPoint);
            var workingArea = currentScreen.WorkingArea;
            
            newX = Math.Max(workingArea.Left, Math.Min(newX, workingArea.Right - Width));
            newY = Math.Max(workingArea.Top, Math.Min(newY, workingArea.Bottom - Height));
            
            // 计算移动距离，超过阈值才算拖动
            var deltaX = Math.Abs(newX - _dragStartPosition.X);
            var deltaY = Math.Abs(newY - _dragStartPosition.Y);
            var distance = Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
            
            if (distance > DragThreshold)
            {
                _moved = true;
            }
            
            Left = newX;
            Top = newY;
        }
        catch
        {
            // 忽略拖动过程中的异常
        }
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        
        _dragging = false;
        
        // 释放鼠标捕获
        ReleaseMouseCapture();
        
        if (!_moved)
        {
            // 点击事件：恢复主窗口
            RestoreRequested?.Invoke();
        }
        else
        {
            // 拖动结束：延迟吸附到边缘，避免卡顿
            _ = Dispatcher.InvokeAsync(new Action(() =>
            {
                try
                {
                    var center = new System.Windows.Point(Left + Width / 2, Top + Height / 2);
                    PlaceNear(center);
                }
                catch
                {
                    // 忽略吸附过程中的异常
                }
            }), System.Windows.Threading.DispatcherPriority.Background);
        }
        
        _moved = false;
    }

}
