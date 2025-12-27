using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace ClassroomToolkit.App.Paint;

public partial class PaintToolbarWindow : Window
{
    private const double BaseWidth = 260;
    private const double BaseHeight = 120;
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
    public event Action? SettingsRequested;
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
            _shapeType = settings.ShapeType;
            _boardColor = settings.BoardColor;
            SetQuickColorSlot(0, settings.QuickColor1);
            SetQuickColorSlot(1, settings.QuickColor2);
            SetQuickColorSlot(2, settings.QuickColor3);
            BoardButton.IsChecked = _boardActive;
            UpdateQuickColorSelection(settings.BrushColor);
            ApplyUiScale(settings.PaintToolbarScale);
        }
        finally
        {
            _initializing = false;
        }
        if (!_modeInitialized)
        {
            UpdateToolButtons(PaintToolMode.Brush);
            _modeInitialized = true;
        }
    }

    public void AttachOverlay(PaintOverlayWindow overlay)
    {
        _overlay = overlay;
    }

    private void ApplyUiScale(double scale)
    {
        _uiScale = Math.Max(0.8, Math.Min(2.0, scale));
        if (ToolbarRoot != null)
        {
            ToolbarRoot.LayoutTransform = new ScaleTransform(_uiScale, _uiScale);
        }
        Width = BaseWidth * _uiScale;
        Height = BaseHeight * _uiScale;
        WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnModeClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton button || _initializing)
        {
            return;
        }
        if (button == CursorButton)
        {
            UpdateToolButtons(CursorButton.IsChecked == true ? PaintToolMode.Cursor : PaintToolMode.Brush);
            return;
        }
        if (button == EraserButton)
        {
            UpdateToolButtons(EraserButton.IsChecked == true ? PaintToolMode.Eraser : PaintToolMode.Brush);
            return;
        }
        if (button == RegionEraseButton)
        {
            UpdateToolButtons(RegionEraseButton.IsChecked == true ? PaintToolMode.RegionErase : PaintToolMode.Brush);
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
        UpdateQuickColorSelection(_quickColors[index.Value]);
        UpdateToolButtons(PaintToolMode.Brush);
        if (_overlay != null)
        {
            _overlay.SetMode(PaintToolMode.Brush);
            _overlay.SetBrush(_quickColors[index.Value], _brushSize, _brushOpacity);
        }
        BrushColorChanged?.Invoke(_quickColors[index.Value]);
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

    private void UpdateToolButtons(PaintToolMode mode)
    {
        _initializing = true;
        try
        {
            _currentMode = mode;
            CursorButton.IsChecked = mode == PaintToolMode.Cursor;
            EraserButton.IsChecked = mode == PaintToolMode.Eraser;
            RegionEraseButton.IsChecked = mode == PaintToolMode.RegionErase;
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
        using var dialog = new System.Windows.Forms.ColorDialog();
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }
        var color = MediaColor.FromArgb(dialog.Color.A, dialog.Color.R, dialog.Color.G, dialog.Color.B);
        SetQuickColorSlot(index, color);
        QuickColorSlotChanged?.Invoke(index, color);
        UpdateToolButtons(PaintToolMode.Brush);
        UpdateQuickColorSelection(color);
        if (_overlay != null)
        {
            _overlay.SetMode(PaintToolMode.Brush);
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
        var button = index switch
        {
            0 => QuickColor1Button,
            1 => QuickColor2Button,
            2 => QuickColor3Button,
            _ => null
        };
        if (button == null)
        {
            return;
        }
        button.Background = new SolidColorBrush(color);
        button.Foreground = GetContrastingBrush(color);
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
        for (var i = 0; i < _quickColors.Length && i < buttons.Length; i++)
        {
            var isActive = _quickColors[i].R == match.R
                           && _quickColors[i].G == match.G
                           && _quickColors[i].B == match.B;
            buttons[i].IsChecked = isActive;
        }
    }

    private void OnToolbarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }
        try
        {
            DragMove();
        }
        catch
        {
            // 忽略拖拽异常。
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
}
