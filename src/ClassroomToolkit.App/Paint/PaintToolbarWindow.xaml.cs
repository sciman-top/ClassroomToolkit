using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Windowing;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;

namespace ClassroomToolkit.App.Paint;

public partial class PaintToolbarWindow : Window
{
    private bool _initializing;
    private readonly MediaColor[] _quickColors = new MediaColor[3];
    private double _brushSize = 12;
    private double _eraserSize = 24;
    private byte _brushOpacity = 255;
    private byte _boardOpacity = 255;
    private PaintShapeType _shapeType = PaintShapeType.Line;
    private PaintShapeType _lastShapeType = PaintShapeType.Line;
    private bool _boardActive;
    private MediaColor _boardColor = Colors.White;
    private PaintOverlayWindow? _overlay;
    private double _uiScale = ToolbarScaleDefaults.Default;
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
    public ICommand OpenShapeMenuCommand { get; }

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
        OpenShapeMenuCommand = new RelayCommand(OpenShapeMenu);
        DataContext = this;

        CursorButton.IsChecked = false;
        EraserButton.IsChecked = false;
        RegionEraseButton.IsChecked = false;
        SetQuickColorSlot(0, Colors.Black);
        SetQuickColorSlot(1, Colors.Red);
        SetQuickColorSlot(2, ColorFromHex("#1E90FF", Colors.DodgerBlue));
        UpdateShapeButtonIcon();
        PreviewKeyDown += OnPreviewKeyDown;
        Loaded += OnToolbarLoaded;
        IsVisibleChanged += OnToolbarVisibleChanged;
        Closed += OnToolbarClosed;
    }

    private void OnToolbarLoaded(object sender, RoutedEventArgs e)
    {
        WindowPlacementHelper.EnsureVisible(this);
    }

    private void OnToolbarVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            WindowPlacementHelper.EnsureVisible(this);
        }
    }

    private void OnToolbarClosed(object? sender, EventArgs e)
    {
        PreviewKeyDown -= OnPreviewKeyDown;
        Loaded -= OnToolbarLoaded;
        IsVisibleChanged -= OnToolbarVisibleChanged;
        Closed -= OnToolbarClosed;
    }

    public void ApplySettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        _initializing = true;
        try
        {
            _brushSize = settings.BrushSize;
            _eraserSize = settings.EraserSize;
            _brushOpacity = settings.BrushOpacity;
            _boardOpacity = 255;
            _shapeType = settings.ShapeType;
            if (_shapeType != PaintShapeType.None)
            {
                _lastShapeType = _shapeType;
            }
            _boardColor = settings.BoardColor;
            SetQuickColorSlot(0, settings.QuickColor1);
            SetQuickColorSlot(1, settings.QuickColor2);
            SetQuickColorSlot(2, settings.QuickColor3);
            BoardButton.IsChecked = _boardActive;
            UpdateQuickColorSelection(settings.BrushColor);
            ApplyUiScale(settings.PaintToolbarScale);
            PhotoOpenButton.IsEnabled = true;
            UpdateShapeButtonIcon();
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
        ArgumentNullException.ThrowIfNull(overlay);
        _overlay = overlay;
    }

    private void ApplyUiScale(double scale)
    {
        _uiScale = Math.Max(ToolbarScaleDefaults.Min, Math.Min(ToolbarScaleDefaults.Max, scale));
        if (ToolbarContainer != null)
        {
            ToolbarContainer.LayoutTransform = new ScaleTransform(_uiScale, _uiScale);
        }
        WindowPlacementHelper.EnsureVisible(this);
    }



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
            return;
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
        
        // 始终同步回画笔模式，避免工具高亮状态残留
        UpdateToolButtons(PaintToolMode.Brush);
        
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
        
        SafeActionExecutionExecutor.TryExecute(
            () => BrushColorChanged?.Invoke(selectedColor),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: brush color callback failed: {ex.Message}"));
    }

    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (_overlay != null)
        {
            _overlay.ClearAll();
            return;
        }
        SafeActionExecutionExecutor.TryExecute(
            () => ClearRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: clear callback failed: {ex.Message}"));
    }

    private void OnUndoClick(object sender, RoutedEventArgs e)
    {
        if (_overlay != null)
        {
            _overlay.Undo();
            return;
        }
        SafeActionExecutionExecutor.TryExecute(
            () => UndoRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: undo callback failed: {ex.Message}"));
    }

    private void OnShapeButtonClick(object sender, RoutedEventArgs e)
    {
        var shapeType = ResolveEffectiveShapeType();
        ApplyShapeType(shapeType);
        UpdateToolButtons(PaintToolMode.Shape);
    }

    private void OpenShapeMenu()
    {
        if (ShapeButton.ContextMenu == null)
        {
            return;
        }
        ShapeButton.ContextMenu.PlacementTarget = ShapeButton;
        ShapeButton.ContextMenu.IsOpen = true;
    }

    private void OnShapeMenuItemClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.Tag is not string tag)
        {
            return;
        }
        if (!Enum.TryParse<PaintShapeType>(tag, ignoreCase: true, out var type))
        {
            return;
        }

        ApplyShapeType(type);
        UpdateToolButtons(PaintToolMode.Shape);
    }

    private void OnBoardClick(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }
        _boardActive = BoardButton.IsChecked == true;
        ApplyBoardState();
    }

    private void OnPhotoOpenClick(object sender, RoutedEventArgs e)
    {
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoOpenRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: photo open callback failed: {ex.Message}"));
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
        SafeActionExecutionExecutor.TryExecute(
            () => ModeChanged?.Invoke(mode),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: mode callback failed: {ex.Message}"));
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
        if (!TryShowDialogWithDiagnostics(dialog, nameof(BoardColorDialog)) || dialog.SelectedColor == null)
        {
            return;
        }
        var color = dialog.SelectedColor.Value;
        ApplyBoardColor(color);
        SafeActionExecutionExecutor.TryExecute(
            () => BoardColorChanged?.Invoke(color),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: board color callback failed: {ex.Message}"));
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

    public void SetBoardActive(bool active)
    {
        if (_boardActive == active)
        {
            return;
        }
        _initializing = true;
        BoardButton.IsChecked = active;
        _initializing = false;
        _boardActive = active;
        ApplyBoardState();
    }

    private void ApplyBoardState()
    {
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
        SafeActionExecutionExecutor.TryExecute(
            () => WhiteboardToggled?.Invoke(_boardActive),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: whiteboard callback failed: {ex.Message}"));
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
        if (!TryShowDialogWithDiagnostics(picker, nameof(QuickColorPaletteWindow)) || picker.SelectedColor == null)
        {
            return;
        }
        var color = picker.SelectedColor.Value;
        SetQuickColorSlot(index, color);
        SafeActionExecutionExecutor.TryExecute(
            () => QuickColorSlotChanged?.Invoke(index, color),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: quick color callback failed: {ex.Message}"));
        
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
        
        SafeActionExecutionExecutor.TryExecute(
            () => BrushColorChanged?.Invoke(color),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: brush color callback failed: {ex.Message}"));
    }

    private bool TryShowDialogWithDiagnostics(Window dialog, string dialogName)
    {
        var result = false;
        SafeActionExecutionExecutor.TryExecute(
            () => result = dialog.SafeShowDialog() == true,
            ex => System.Diagnostics.Debug.WriteLine(
                DialogShowDiagnosticsPolicy.FormatFailureMessage(
                    dialogName,
                    ex.Message)));
        return result;
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
        return PaintActionInvoker.TryInvoke(() =>
        {
            return (MediaColor)MediaColorConverter.ConvertFromString(value);
        }, fallback);
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        SafeActionExecutionExecutor.TryExecute(
            () => SettingsRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: settings callback failed: {ex.Message}"));
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
        UpdateShapeButtonIcon();
        if (_overlay != null)
        {
            _overlay.SetShapeType(_shapeType);
        }
        SafeActionExecutionExecutor.TryExecute(
            () => ShapeTypeChanged?.Invoke(_shapeType),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: shape callback failed: {ex.Message}"));
    }

    private PaintShapeType ResolveEffectiveShapeType()
    {
        if (_shapeType != PaintShapeType.None)
        {
            return _shapeType;
        }

        return _lastShapeType == PaintShapeType.None
            ? PaintShapeType.Line
            : _lastShapeType;
    }

    private void ApplyShapeType(PaintShapeType type)
    {
        var normalized = NormalizeShapeType(type);
        if (normalized == PaintShapeType.None)
        {
            return;
        }

        _shapeType = normalized;
        _lastShapeType = normalized;
        UpdateShapeButtonIcon();
        if (_overlay != null)
        {
            _overlay.SetShapeType(_shapeType);
        }
        SafeActionExecutionExecutor.TryExecute(
            () => ShapeTypeChanged?.Invoke(_shapeType),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: shape callback failed: {ex.Message}"));
    }

    private static PaintShapeType NormalizeShapeType(PaintShapeType type)
    {
        return type switch
        {
            PaintShapeType.RectangleFill => PaintShapeType.RectangleFill,
            PaintShapeType.None => PaintShapeType.None,
            _ => type
        };
    }

    private void UpdateShapeButtonIcon()
    {
        if (ShapeButtonIconPath == null)
        {
            return;
        }

        var type = ResolveEffectiveShapeType();
        var resourceKey = type switch
        {
            PaintShapeType.DashedLine => "Icon_ShapeLineDashed",
            PaintShapeType.Arrow => "Icon_ShapeArrowSolid",
            PaintShapeType.DashedArrow => "Icon_ShapeArrowDashed",
            PaintShapeType.Rectangle => "Icon_ShapeRectOutline",
            PaintShapeType.RectangleFill => "Icon_ShapeRectFill",
            PaintShapeType.Ellipse => "Icon_ShapeEllipse",
            PaintShapeType.Triangle => "Icon_ShapeTriangle",
            _ => "Icon_ShapeLineSolid"
        };

        if (TryFindResource(resourceKey) is Geometry geometry)
        {
            ShapeButtonIconPath.Data = geometry;
        }
    }

    private void OnToolbarDrag(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            PaintActionInvoker.TryInvoke(DragMove);
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
        var overlay = _overlay;
        if (overlay == null)
        {
            return;
        }

        e.Handled = AuxWindowKeyRoutingHandler.TryHandle(
            key,
            overlayVisible: overlay.IsVisible,
            tryHandlePhotoKey: overlay.TryHandlePhotoKey,
            canRoutePresentationInput: overlay.CanRoutePresentationInputFromAuxWindow(),
            forwardPresentationKey: overlay.ForwardKeyboardToPresentation);
    }

}
