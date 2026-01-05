using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace ClassroomToolkit.App.Paint;

public partial class PaintToolbarWindow : Window
{
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);
    private const int GwlExstyle = -20;
    private const int WsExNoActivate = 0x08000000;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;
    private IntPtr _hwnd;
    private bool _initializing;
    private readonly MediaColor[] _quickColors = new MediaColor[3];
    private double _brushSize = 12;
    private double _eraserSize = 24;
    private byte _brushOpacity = 255;
    private byte _boardOpacity = 255;
    private PaintShapeType _shapeType = PaintShapeType.Line;
    private bool _boardActive;
    private MediaColor _boardColor = Colors.White;
    private PaintOverlayWindow? _overlay;
    private double _uiScale = 1.0;
    private bool _modeInitialized;
    private PaintToolMode _currentMode = PaintToolMode.Brush;
    public event Action<PaintToolMode>? ModeChanged;
    public event Action<MediaColor>? BrushColorChanged;
    public event Action<MediaColor>? BoardColorChanged;
    public event Action? ClearRequested;
    public event Action? UndoRequested;
    public event Action<int, MediaColor>? QuickColorSlotChanged;
    public event Action<PaintShapeType>? ShapeTypeChanged;
    public event Action? SettingsRequested;
    public event Action? PhotoOpenRequested;
    public event Action<bool>? WhiteboardToggled;

    public ICommand OpenBoardColorCommand { get; }
    public ICommand OpenQuickColor1Command { get; }
    public ICommand OpenQuickColor2Command { get; }
    public ICommand OpenQuickColor3Command { get; }

    public double BrushSize => _brushSize;
    public double EraserSize => _eraserSize;
    public byte BrushOpacity => _brushOpacity;
    public byte BoardOpacity => _boardOpacity;
    public PaintShapeType ShapeType => _shapeType;
    public bool BoardActive => _boardActive;
    public MediaColor BoardColor => _boardColor;
    public bool HasOverlay => _overlay != null;
    public PaintToolMode CurrentMode => _currentMode;

    public PaintToolbarWindow()
    {
        InitializeComponent();
        OpenBoardColorCommand = new RelayCommand(OpenBoardColorDialog);
        OpenQuickColor1Command = new RelayCommand(() => OpenQuickColorDialog(0));
        OpenQuickColor2Command = new RelayCommand(() => OpenQuickColorDialog(1));
        OpenQuickColor3Command = new RelayCommand(() => OpenQuickColorDialog(2));
        DataContext = this;

        CursorButton.IsChecked = false;
        EraserButton.IsChecked = false;
        RegionEraseButton.IsChecked = false;
        SetQuickColorSlot(0, Colors.Black);
        SetQuickColorSlot(1, Colors.Red);
        SetQuickColorSlot(2, ColorFromHex("#1E90FF", Colors.DodgerBlue));
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            // 不再应用 WS_EX_NOACTIVATE，以允许工具栏窗口正常获得焦点和用户交互
            // ApplyNoActivate();
        };
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += (_, _) => WindowPlacementHelper.EnsureVisible(this);
        IsVisibleChanged += (_, _) =>
        {
            if (IsVisible)
            {
                WindowPlacementHelper.EnsureVisible(this);
            }
        };
    }

    public void ApplySettings(AppSettings settings)
    {
        _initializing = true;
        try
        {
            _brushSize = settings.BrushSize;
            _eraserSize = settings.EraserSize;
            _brushOpacity = settings.BrushOpacity;
            _boardOpacity = 255;
            _shapeType = settings.ShapeType == PaintShapeType.RectangleFill
                ? PaintShapeType.Rectangle
                : settings.ShapeType;
            _boardColor = settings.BoardColor;
            SetQuickColorSlot(0, settings.QuickColor1);
            SetQuickColorSlot(1, settings.QuickColor2);
            SetQuickColorSlot(2, settings.QuickColor3);
            BoardButton.IsChecked = _boardActive;
            UpdateQuickColorSelection(settings.BrushColor);
            ApplyUiScale(settings.PaintToolbarScale);
            PhotoOpenButton.IsEnabled = true;
        }
        finally
        {
            _initializing = false;
        }
        if (!_modeInitialized)
        {
            UpdateToolButtons(_shapeType == PaintShapeType.None ? PaintToolMode.Brush : PaintToolMode.Shape);
            _modeInitialized = true;
            return;
        }
        if (_shapeType == PaintShapeType.None && _currentMode == PaintToolMode.Shape)
        {
            UpdateToolButtons(PaintToolMode.Brush);
        }
        else if (_shapeType != PaintShapeType.None && _currentMode == PaintToolMode.Brush)
        {
            UpdateToolButtons(PaintToolMode.Shape);
        }
    }

    public void AttachOverlay(PaintOverlayWindow overlay)
    {
        _overlay = overlay;
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

    private void ApplyUiScale(double scale)
    {
        _uiScale = Math.Max(0.8, Math.Min(2.0, scale));
        if (ToolbarContainer != null)
        {
            ToolbarContainer.LayoutTransform = new ScaleTransform(_uiScale, _uiScale);
        }
        WindowPlacementHelper.EnsureVisible(this);
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

    private void OnModeChecked(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton || _initializing)
        {
            return;
        }
        if (ReferenceEquals(sender, CursorButton))
        {
            UpdateToolButtons(PaintToolMode.Cursor);
            return;
        }
        if (ReferenceEquals(sender, EraserButton))
        {
            UpdateToolButtons(PaintToolMode.Eraser);
            return;
        }
        if (ReferenceEquals(sender, RegionEraseButton))
        {
            UpdateToolButtons(PaintToolMode.RegionErase);
        }
    }

    private void OnModeUnchecked(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }
        if (ReferenceEquals(sender, CursorButton) && _currentMode == PaintToolMode.Cursor)
        {
            UpdateToolButtons(PaintToolMode.Brush);
            return;
        }
        if (ReferenceEquals(sender, EraserButton) && _currentMode == PaintToolMode.Eraser)
        {
            UpdateToolButtons(PaintToolMode.Brush);
            return;
        }
        if (ReferenceEquals(sender, RegionEraseButton) && _currentMode == PaintToolMode.RegionErase)
        {
            UpdateToolButtons(PaintToolMode.Brush);
        }
    }

    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button)
        {
            return;
        }
        
        var index = ResolveQuickColorIndex(button.Tag);
        if (!index.HasValue || index.Value < 0 || index.Value >= _quickColors.Length)
        {
            return;
        }
        
        var shouldResetShape = _shapeType != PaintShapeType.None;
        var selectedColor = _quickColors[index.Value];
        
        // 更新颜色选择状态
        UpdateQuickColorSelection(selectedColor);
        
        // 如果当前不是画笔模式，切换到画笔模式
        if (_currentMode != PaintToolMode.Brush)
        {
            UpdateToolButtons(PaintToolMode.Brush);
        }
        
        // 重置形状类型（如果需要）
        if (shouldResetShape)
        {
            ResetShapeType();
        }
        
        // 应用画笔设置
        if (_overlay != null)
        {
            _overlay.SetBrush(selectedColor, _brushSize, _brushOpacity);
        }
        
        BrushColorChanged?.Invoke(selectedColor);
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (_overlay != null)
        {
            _overlay.ClearAll();
            return;
        }
        ClearRequested?.Invoke();
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        if (_overlay != null)
        {
            _overlay.Undo();
            return;
        }
        UndoRequested?.Invoke();
    }

    private void OnBoardClick(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }
        _boardActive = BoardButton.IsChecked == true;
        if (_overlay != null)
        {
            if (_boardActive)
            {
                _overlay.SetBoardColor(_boardColor);
                _overlay.SetBoardOpacity(255);
            }
            else
            {
                _overlay.SetBoardColor(Colors.Transparent);
                _overlay.SetBoardOpacity(0);
            }
        }
        WhiteboardToggled?.Invoke(_boardActive);
    }

    private void OnPhotoOpenClick(object sender, RoutedEventArgs e)
    {
        PhotoOpenRequested?.Invoke();
    }

    private void UpdateToolButtons(PaintToolMode mode)
    {
        _initializing = true;
        try
        {
            _currentMode = mode;
            
            // 首先重置所有工具按钮状态
            CursorButton.IsChecked = false;
            EraserButton.IsChecked = false;
            RegionEraseButton.IsChecked = false;
            
            // 然后设置当前模式的按钮状态
            switch (mode)
            {
                case PaintToolMode.Cursor:
                    CursorButton.IsChecked = true;
                    break;
                case PaintToolMode.Eraser:
                    EraserButton.IsChecked = true;
                    break;
                case PaintToolMode.RegionErase:
                    RegionEraseButton.IsChecked = true;
                    break;
                case PaintToolMode.Brush:
                case PaintToolMode.Shape:
                    // 画笔和形状模式不选中任何工具按钮，但保持颜色按钮状态
                    break;
            }
            
            // 只有在非画笔/形状模式时才清除颜色按钮选择
            if (mode != PaintToolMode.Brush && mode != PaintToolMode.Shape)
            {
                QuickColor1Button.IsChecked = false;
                QuickColor2Button.IsChecked = false;
                QuickColor3Button.IsChecked = false;
            }
        }
        finally
        {
            _initializing = false;
        }
        ModeChanged?.Invoke(mode);
        if (_overlay != null)
        {
            _overlay.SetMode(mode);
        }
    }

    private void OpenBoardColorDialog()
    {
        var dialog = new BoardColorDialog
        {
            Owner = this
        };
        if (dialog.ShowDialog() != true || dialog.SelectedColor == null)
        {
            return;
        }
        var color = dialog.SelectedColor.Value;
        ApplyBoardColor(color);
        BoardColorChanged?.Invoke(color);
    }

    private void ApplyBoardColor(MediaColor color)
    {
        _boardColor = color;
        if (_overlay != null && _boardActive)
        {
            _overlay.SetBoardColor(color);
            _overlay.SetBoardOpacity(255);
        }
    }

    private void OpenQuickColorDialog(int index)
    {
        var picker = new QuickColorPaletteWindow
        {
            Owner = this
        };
        var button = GetQuickColorButton(index);
        if (button != null)
        {
            var anchor = button.PointToScreen(new System.Windows.Point(0, button.ActualHeight + 4));
            picker.Left = anchor.X;
            picker.Top = anchor.Y;
        }
        if (picker.ShowDialog() != true || picker.SelectedColor == null)
        {
            return;
        }
        var color = picker.SelectedColor.Value;
        SetQuickColorSlot(index, color);
        QuickColorSlotChanged?.Invoke(index, color);
        
        // 如果当前是形状模式，重置形状类型
        if (_currentMode == PaintToolMode.Shape)
        {
            ResetShapeType();
        }
        
        // 如果当前不是画笔模式，切换到画笔模式
        if (_currentMode != PaintToolMode.Brush)
        {
            UpdateToolButtons(PaintToolMode.Brush);
        }
        
        // 更新颜色选择状态
        UpdateQuickColorSelection(color);
        
        // 应用画笔设置
        if (_overlay != null)
        {
            _overlay.SetBrush(color, _brushSize, _brushOpacity);
        }
        
        BrushColorChanged?.Invoke(color);
    }

    private void SetQuickColorSlot(int index, MediaColor color)
    {
        if (index < 0 || index >= _quickColors.Length)
        {
            return;
        }
        _quickColors[index] = color;
        UpdateQuickColorButton(index, color);
    }

    private void UpdateQuickColorButton(int index, MediaColor color)
    {
        var button = GetQuickColorButton(index);
        if (button == null)
        {
            return;
        }
        button.Background = new SolidColorBrush(color);
        button.Foreground = GetContrastingBrush(color);
    }

    private ToggleButton? GetQuickColorButton(int index)
    {
        return index switch
        {
            0 => QuickColor1Button,
            1 => QuickColor2Button,
            2 => QuickColor3Button,
            _ => null
        };
    }

    private static System.Windows.Media.Brush GetContrastingBrush(MediaColor color)
    {
        var luminance = (0.299 * color.R + 0.587 * color.G + 0.114 * color.B) / 255.0;
        return luminance > 0.6 ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.White;
    }

    private static int? ResolveQuickColorIndex(object? tag)
    {
        if (tag is int index)
        {
            return index;
        }
        if (tag is string text && int.TryParse(text, out var parsed))
        {
            return parsed;
        }
        return null;
    }

    private static MediaColor ColorFromHex(string value, MediaColor fallback)
    {
        try
        {
            return (MediaColor)MediaColorConverter.ConvertFromString(value);
        }
        catch
        {
            return fallback;
        }
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SettingsRequested?.Invoke();
    }

    private void UpdateQuickColorSelection(MediaColor color)
    {
        var match = color;
        var buttons = new[] { QuickColor1Button, QuickColor2Button, QuickColor3Button };
        var matched = false;
        for (var i = 0; i < _quickColors.Length && i < buttons.Length; i++)
        {
            var isActive = _quickColors[i].R == match.R
                           && _quickColors[i].G == match.G
                           && _quickColors[i].B == match.B;
            if (isActive && !matched)
            {
                buttons[i].IsChecked = true;
                matched = true;
            }
            else
            {
                buttons[i].IsChecked = false;
            }
        }
    }

    private void ResetShapeType()
    {
        if (_shapeType == PaintShapeType.None)
        {
            return;
        }
        _shapeType = PaintShapeType.None;
        if (_overlay != null)
        {
            _overlay.SetShapeType(_shapeType);
        }
        ShapeTypeChanged?.Invoke(_shapeType);
    }

    private void OnToolbarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try
            {
                DragMove();
            }
            catch
            {
                // 忽略异常
            }
        }
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }
            current = GetParent(current);
        }
        return false;
    }

    private static DependencyObject? GetParent(DependencyObject obj)
    {
        if (obj is System.Windows.Documents.TextElement textElement)
        {
            return textElement.Parent;
        }
        if (obj is FrameworkContentElement contentElement)
        {
            return contentElement.Parent;
        }
        var parent = VisualTreeHelper.GetParent(obj);
        if (parent == null && obj is FrameworkElement element)
        {
            parent = element.Parent as DependencyObject;
        }
        return parent ?? LogicalTreeHelper.GetParent(obj);
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var key = e.Key;
        if (_overlay != null && _overlay.TryHandleReviewNavigationKey(key))
        {
            e.Handled = true;
            return;
        }
        // 只在光标模式下转发键盘事件到演示文稿
        if (_currentMode != PaintToolMode.Cursor)
        {
            return;
        }
        // 只转发演示文稿导航键
        bool isNavigationKey = key == System.Windows.Input.Key.Left ||
                               key == System.Windows.Input.Key.Right ||
                               key == System.Windows.Input.Key.Up ||
                               key == System.Windows.Input.Key.Down ||
                               key == System.Windows.Input.Key.PageUp ||
                               key == System.Windows.Input.Key.PageDown ||
                               key == System.Windows.Input.Key.Space ||
                               key == System.Windows.Input.Key.Enter ||
                               key == System.Windows.Input.Key.Home ||
                               key == System.Windows.Input.Key.End;

        if (!isNavigationKey)
        {
            return;
        }
        // 通知覆盖窗口转发到演示文稿
        _overlay?.ForwardKeyboardToPresentation(key);
        e.Handled = true;
    }

    private void ApplyNoActivate()
    {
        if (_hwnd == IntPtr.Zero)
        {
            return;
        }
        var exStyle = GetWindowLong(_hwnd, GwlExstyle);
        SetWindowLong(_hwnd, GwlExstyle, exStyle | WsExNoActivate);
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
}
