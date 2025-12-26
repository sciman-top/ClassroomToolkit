using System.Windows;
using System.Windows.Input;

namespace ClassroomToolkit.App;

public partial class LauncherBubbleWindow : Window
{
    private bool _dragging;
    private bool _moved;
    private System.Windows.Point _dragOffset;

    public LauncherBubbleWindow()
    {
        InitializeComponent();
        Cursor = Cursors.Hand;
        MouseLeftButtonDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseLeftButtonUp += OnMouseUp;
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

    private void OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        _dragging = true;
        _moved = false;
        _dragOffset = e.GetPosition(this);
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (!_dragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        var screen = PointToScreen(e.GetPosition(this));
        Left = screen.X - _dragOffset.X;
        Top = screen.Y - _dragOffset.Y;
        _moved = true;
    }

    private void OnMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (!_moved)
        {
            RestoreRequested?.Invoke();
        }
        else
        {
            var center = new System.Windows.Point(Left + Width / 2, Top + Height / 2);
            PlaceNear(center);
        }
        _dragging = false;
    }
}
