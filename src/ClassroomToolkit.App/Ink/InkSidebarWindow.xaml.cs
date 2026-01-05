using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App.Ink;

public partial class InkSidebarWindow : Window
{
    private const int GwlExstyle = -20;
    private const int WsExTransparent = 0x20;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpShowWindow = 0x0040;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);

    private readonly InkStorageService _storage;
    private readonly List<int> _pageIndices;
    private bool _hovering;
    private IntPtr _hwnd;
    private string _documentName = string.Empty;
    private DateTime _currentDate = DateTime.Today;
    private int _currentPageIndex = 1;
    private readonly System.Windows.Threading.DispatcherTimer _hoverCheckTimer;

    public event Action<int>? PageSelected;
    public event Action? HistoryRequested;
    public event Action? SettingsRequested;

    public InkSidebarWindow()
    {
        InitializeComponent();
        _storage = new InkStorageService();
        _pageIndices = new List<int>();
        PreviewKeyDown += OnPreviewKeyDown;
        PreviewMouseWheel += OnPreviewMouseWheel;

        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            UpdateWindowTransparency();
            SyncTopmost(true);
        };
        _hoverCheckTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(120)
        };
        _hoverCheckTimer.Tick += (_, _) => UpdateHoverState();
        PaintModeManager.Instance.PaintModeChanged += _ => UpdateWindowTransparency();
        PaintModeManager.Instance.IsDrawingChanged += _ => UpdateWindowTransparency();
    }

    public void UpdateContext(string documentName, DateTime date, int pageIndex)
    {
        _documentName = documentName ?? string.Empty;
        _currentDate = date;
        _currentPageIndex = Math.Max(1, pageIndex);
        ReloadPages();
    }

    public void SyncTopmost(bool enabled)
    {
        Topmost = enabled;
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var insertAfter = enabled ? HwndTopmost : HwndNoTopmost;
        SetWindowPos(_hwnd, insertAfter, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
    }

    private void ReloadPages()
    {
        _pageIndices.Clear();
        if (string.IsNullOrWhiteSpace(_documentName))
        {
            return;
        }
        var pages = _storage.ListPages(_currentDate, _documentName);
        foreach (var page in pages)
        {
            _pageIndices.Add(page.PageIndex);
        }
        UpdateCurrentPageIndex();
    }

    private void OnHistoryClick(object sender, RoutedEventArgs e)
    {
        HistoryRequested?.Invoke();
    }

    private void OnPreviousClick(object sender, RoutedEventArgs e)
    {
        NavigateByOffset(-1);
    }

    private void OnNextClick(object sender, RoutedEventArgs e)
    {
        NavigateByOffset(1);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
    }

    public void NavigatePrevious()
    {
        NavigateByOffset(-1);
    }

    public void NavigateNext()
    {
        NavigateByOffset(1);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key;
        int? direction = key switch
        {
            Key.Right => 1,
            Key.Down => 1,
            Key.PageDown => 1,
            Key.Space => 1,
            Key.Enter => 1,
            Key.Left => -1,
            Key.Up => -1,
            Key.PageUp => -1,
            _ => null
        };
        if (direction == null)
        {
            return;
        }
        if (direction.Value > 0)
        {
            NavigateNext();
        }
        else
        {
            NavigatePrevious();
        }
        e.Handled = true;
    }

    private void OnPreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (e.Delta < 0)
        {
            NavigateNext();
        }
        else
        {
            NavigatePrevious();
        }
        e.Handled = true;
    }

    private void OnTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        try
        {
            DragMove();
        }
        catch
        {
            // Ignore drag exceptions.
        }
    }

    private void NavigateByOffset(int offset)
    {
        if (_pageIndices.Count == 0)
        {
            return;
        }
        var ordered = _pageIndices.OrderBy(page => page).ToList();
        var currentIndex = ordered.FindIndex(page => page == _currentPageIndex);
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }
        var nextIndex = Math.Clamp(currentIndex + offset, 0, ordered.Count - 1);
        var nextPage = ordered[nextIndex];
        if (nextPage == _currentPageIndex)
        {
            return;
        }
        _currentPageIndex = nextPage;
        PageSelected?.Invoke(nextPage);
    }

    private void UpdateCurrentPageIndex()
    {
        if (_pageIndices.Count == 0)
        {
            return;
        }
        if (_pageIndices.Contains(_currentPageIndex))
        {
            return;
        }
        _currentPageIndex = _pageIndices.Max();
    }

    private void UpdateWindowTransparency()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var allowTransparent = !_hovering && PaintModeManager.Instance.ShouldAllowTransparency(isToolbar: false);
        UpdateHoverTimer(allowTransparent);
        var exStyle = GetWindowLong(_hwnd, GwlExstyle);
        if (allowTransparent)
        {
            exStyle |= WsExTransparent;
        }
        else
        {
            exStyle &= ~WsExTransparent;
        }
        SetWindowLong(_hwnd, GwlExstyle, exStyle);
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
        if (!GetCursorPos(out var point))
        {
            return;
        }
        if (!GetWindowRect(_hwnd, out var rect))
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

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hwnd,
        IntPtr hwndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint flags);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hwnd, out NativeRect rect);

    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

}
