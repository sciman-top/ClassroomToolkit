using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using ClassroomToolkit.App.Commands;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Session;
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
    private readonly PaintToolSelectionManager _toolSelectionManager = new(PaintToolMode.Brush);
    private bool _directWhiteboardEntryArmed;
    private readonly DispatcherTimer _regionCaptureResumeTimer;
    private bool _resumeRegionCaptureArmed;
    private bool _regionCapturePending;
    private bool _toolbarDragging;
    private System.Windows.Point _toolbarDragOffset;
    private IDisposable? _toolbarDragScope;
    private bool _toolbarTouchDragging;
    private TouchDevice? _activeToolbarTouchDevice;
    private System.Windows.Point _toolbarTouchDragOffset;
    private System.Drawing.Point? _lastInteractionScreenPoint;
    private ToolbarSecondTapTarget _pendingSecondTapTarget;
    private int? _pendingQuickColorIndex;
    private BoardPrimaryAction _lastBoardPrimaryAction = BoardPrimaryAction.CaptureRegion;
    public event Action<PaintToolMode>? ModeChanged;
    public event Action<MediaColor>? BrushColorChanged;
    public event Action<MediaColor>? BoardColorChanged;
    public event Action? ClearRequested;
    public event Action? UndoRequested;
    public event Action<int, MediaColor>? QuickColorSlotChanged;
    public event Action<PaintShapeType>? ShapeTypeChanged;
    public event Action? SettingsRequested;
    public event Action? PhotoOpenRequested;
    public event Action? RegionCaptureRequested;
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
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewTouchDown += OnPreviewTouchDown;
        PreviewMouseWheel += OnPreviewMouseWheel;
        MouseEnter += OnToolbarMouseEnter;
        MouseLeave += OnToolbarMouseLeave;
        MouseMove += OnToolbarDragMove;
        MouseLeftButtonUp += OnToolbarDragEnd;
        TouchMove += OnToolbarTouchDragMove;
        PreviewTouchUp += OnToolbarTouchDragEnd;
        LostTouchCapture += OnToolbarTouchLostCapture;
        Loaded += OnToolbarLoaded;
        IsVisibleChanged += OnToolbarVisibleChanged;
        Closed += OnToolbarClosed;
        _regionCaptureResumeTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _regionCaptureResumeTimer.Tick += OnRegionCaptureResumeTimerTick;
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
        _regionCaptureResumeTimer.Stop();
        _regionCaptureResumeTimer.Tick -= OnRegionCaptureResumeTimerTick;
        EndToolbarDragCore();
        EndToolbarTouchDragCore();
        PreviewKeyDown -= OnPreviewKeyDown;
        PreviewMouseDown -= OnPreviewMouseDown;
        PreviewTouchDown -= OnPreviewTouchDown;
        PreviewMouseWheel -= OnPreviewMouseWheel;
        MouseEnter -= OnToolbarMouseEnter;
        MouseLeave -= OnToolbarMouseLeave;
        MouseMove -= OnToolbarDragMove;
        MouseLeftButtonUp -= OnToolbarDragEnd;
        TouchMove -= OnToolbarTouchDragMove;
        PreviewTouchUp -= OnToolbarTouchDragEnd;
        LostTouchCapture -= OnToolbarTouchLostCapture;
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
            ForceToolMode(_shapeType == PaintShapeType.None ? PaintToolMode.Brush : PaintToolMode.Shape);
            _modeInitialized = true;
            return;
        }
        if (_shapeType == PaintShapeType.None && _currentMode == PaintToolMode.Shape)
        {
            SelectToolMode(PaintToolMode.Brush, allowToggleOffCurrent: false);
        }
        else if (_shapeType != PaintShapeType.None && _currentMode == PaintToolMode.Brush)
        {
            SelectToolMode(PaintToolMode.Shape, allowToggleOffCurrent: false);
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
        ApplyToolbarTouchMetrics();
        WindowPlacementHelper.EnsureVisible(this);
    }

    private void ApplyToolbarTouchMetrics()
    {
        var scale = Math.Max(_uiScale, ToolbarScaleDefaults.Min);
        var minimumHitTarget = Math.Ceiling(44.0 / scale);
        var minimumColorHitTarget = Math.Ceiling(44.0 / scale);
        var dragHandleHitWidth = Math.Ceiling(36.0 / scale);

        ApplyToolbarButtonMetrics(CursorButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(EraserButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(RegionEraseButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(ClearButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(UndoButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(ShapeButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(PhotoOpenButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(BoardButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(SettingsButton, visualSize: 30, minimumHitTarget);
        ApplyToolbarButtonMetrics(QuickColor1Button, visualSize: 24, minimumColorHitTarget);
        ApplyToolbarButtonMetrics(QuickColor2Button, visualSize: 24, minimumColorHitTarget);
        ApplyToolbarButtonMetrics(QuickColor3Button, visualSize: 24, minimumColorHitTarget);

        if (ToolbarDragHandle != null)
        {
            ToolbarDragHandle.MinWidth = dragHandleHitWidth;
        }
    }

    private static void ApplyToolbarButtonMetrics(System.Windows.Controls.Control? control, double visualSize, double minimumHitTarget)
    {
        if (control == null)
        {
            return;
        }

        control.Width = visualSize;
        control.Height = visualSize;
        control.MinWidth = Math.Max(visualSize, minimumHitTarget);
        control.MinHeight = Math.Max(visualSize, minimumHitTarget);
    }



    private void OnModeButtonClick(object sender, RoutedEventArgs e)
    {
        if (sender is not ToggleButton || _initializing)
        {
            return;
        }
        ResetPendingSecondTapState();
        PrepareForNonBoardToolbarAction(exitWhiteboard: true);
        if (ReferenceEquals(sender, CursorButton))
        {
            SelectToolMode(PaintToolMode.Cursor, allowToggleOffCurrent: false);
            return;
        }
        if (ReferenceEquals(sender, EraserButton))
        {
            SelectToolMode(PaintToolMode.Eraser, allowToggleOffCurrent: true);
            return;
        }
        if (ReferenceEquals(sender, RegionEraseButton))
        {
            SelectToolMode(PaintToolMode.RegionErase, allowToggleOffCurrent: true);
            return;
        }
    }


    private void OnClearClick(object sender, RoutedEventArgs e)
    {
        ResetPendingSecondTapState();
        PrepareForNonBoardToolbarAction(exitWhiteboard: false);
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
        ResetPendingSecondTapState();
        PrepareForNonBoardToolbarAction(exitWhiteboard: false);
        if (_overlay != null)
        {
            _overlay.Undo();
            return;
        }
        SafeActionExecutionExecutor.TryExecute(
            () => UndoRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: undo callback failed: {ex.Message}"));
    }


    private void OnShapeMenuItemClick(object sender, RoutedEventArgs e)
    {
        ResetPendingSecondTapState();
        if (sender is not System.Windows.Controls.MenuItem menuItem || menuItem.Tag is not string tag)
        {
            return;
        }
        if (!Enum.TryParse<PaintShapeType>(tag, ignoreCase: true, out var type))
        {
            return;
        }

        PrepareForNonBoardToolbarAction(exitWhiteboard: true);
        ApplyShapeType(type);
        SelectToolMode(PaintToolMode.Shape, allowToggleOffCurrent: false);
    }


    private void ShowBoardHint(string message)
    {
        if (BoardHintBubble == null || BoardHintText == null)
        {
            return;
        }

        BoardHintText.Text = message;
        BoardHintBubble.Visibility = Visibility.Visible;
        BoardHintBubble.Opacity = 1;
        BoardHintBubble.BeginAnimation(UIElement.OpacityProperty, null);

        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(1000))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        fade.Completed += (_, _) =>
        {
            BoardHintBubble.Opacity = 0;
            BoardHintBubble.Visibility = Visibility.Collapsed;
        };
        BoardHintBubble.BeginAnimation(UIElement.OpacityProperty, fade, HandoffBehavior.SnapshotAndReplace);
    }

    private void OnPhotoOpenClick(object sender, RoutedEventArgs e)
    {
        PrepareForNonBoardToolbarAction(exitWhiteboard: false);
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoOpenRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: photo open callback failed: {ex.Message}"));
    }

    private void SelectToolMode(PaintToolMode requestedMode, bool allowToggleOffCurrent)
    {
        var resolved = _toolSelectionManager.Select(requestedMode, allowToggleOffCurrent);
        ApplyToolMode(resolved);
    }

    private void ForceToolMode(PaintToolMode mode)
    {
        _toolSelectionManager.Reset(mode);
        ApplyToolMode(mode);
    }

    private void ApplyToolMode(PaintToolMode mode)
    {
        if (_currentMode == mode)
        {
            return;
        }

        _initializing = true;
        try
        {
            _currentMode = mode;
            
            // 首先重置所有工具按钮状态
            CursorButton.IsChecked = false;
            EraserButton.IsChecked = false;
            RegionEraseButton.IsChecked = false;
            ShapeButton.IsChecked = false;

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
                case PaintToolMode.Shape:
                    ShapeButton.IsChecked = true;
                    break;
                case PaintToolMode.Brush:
                    break;
            }
            
            // 仅画笔模式保留颜色按钮高亮
            if (mode != PaintToolMode.Brush)
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
        if (_overlay != null)
        {
            _overlay.SetBoardColor(color);
            if (_boardActive)
            {
                _overlay.SetBoardOpacity(255);
            }
        }
    }

    public void SetBoardActive(bool active)
    {
        var overlayWhiteboardActive = IsOverlayWhiteboardSceneActive() || _overlay?.IsWhiteboardActive == true;
        if (_boardActive == active && overlayWhiteboardActive == active)
        {
            RefreshBoardButtonVisualState();
            return;
        }
        _boardActive = active;
        ApplyBoardState();
    }

    private void ApplyBoardState()
    {
        if (_overlay != null)
        {
            _overlay.SetBoardColor(_boardColor);
            _overlay.SetBoardOpacity(_boardActive ? (byte)255 : (byte)0);
        }
        SafeActionExecutionExecutor.TryExecute(
            () => WhiteboardToggled?.Invoke(_boardActive),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: whiteboard callback failed: {ex.Message}"));

        // Keep toolbar state synchronized with overlay session scene.
        var overlaySceneAfterApply = IsOverlayWhiteboardSceneActive();
        if (overlaySceneAfterApply != _boardActive)
        {
            _boardActive = overlaySceneAfterApply;
        }

        RefreshBoardButtonVisualState();
    }

    public void ArmDirectWhiteboardEntry()
    {
        _regionCapturePending = false;
        _directWhiteboardEntryArmed = true;
        _resumeRegionCaptureArmed = true;
        if (!_regionCaptureResumeTimer.IsEnabled)
        {
            _regionCaptureResumeTimer.Start();
        }
        RefreshBoardButtonVisualState();
    }

    public void ClearDirectWhiteboardEntryArm()
    {
        _regionCapturePending = false;
        _directWhiteboardEntryArmed = false;
        _resumeRegionCaptureArmed = false;
        _regionCaptureResumeTimer.Stop();
        RefreshBoardButtonVisualState();
    }

    private void OnRegionCaptureResumeTimerTick(object? sender, EventArgs e)
    {
        if (!_resumeRegionCaptureArmed)
        {
            _regionCaptureResumeTimer.Stop();
            return;
        }
        if (!IsVisible || !IsLoaded)
        {
            return;
        }
        if (BoardActive || _overlay?.IsWhiteboardActive == true)
        {
            ClearDirectWhiteboardEntryArm();
            return;
        }

        TryResumeRegionCaptureIfPointerOutsideToolbar();
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        RecordInteractionScreenPoint(e.GetPosition(this));

        var button = FindButtonBase(e.OriginalSource as DependencyObject);
        if (button == null)
        {
            return;
        }

        var captureInteractionActive = _resumeRegionCaptureArmed || _directWhiteboardEntryArmed || _regionCapturePending;
        if (!captureInteractionActive)
        {
            return;
        }

        if (ReferenceEquals(button, BoardButton))
        {
            RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarHandledPress();
            return;
        }

        if (!ToolbarResumeCancellationPolicy.ShouldCancelPendingResumeOnToolbarPress(
                captureInteractionActive,
                pressedToolbarButton: true,
                pressedBoardButton: ReferenceEquals(button, BoardButton)))
        {
            return;
        }

        RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarHandledPress();
        ClearDirectWhiteboardEntryArm();
    }

    private void OnPreviewTouchDown(object? sender, TouchEventArgs e)
    {
        RecordInteractionScreenPoint(e.GetTouchPoint(this).Position);
    }

    public System.Drawing.Point? TryGetLastInteractionScreenPoint()
    {
        return _lastInteractionScreenPoint;
    }

    private void OnToolbarMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarPointerMove();
    }

    private void OnToolbarMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        TryResumeRegionCaptureIfPointerOutsideToolbar();
    }

    private bool IsPointInsideToolbar(double screenX, double screenY)
    {
        if (!TryResolveScreenBounds(out var bounds))
        {
            return false;
        }

        return screenX >= bounds.Left
            && screenX <= bounds.Right
            && screenY >= bounds.Top
            && screenY <= bounds.Bottom;
    }

    public bool TryActivateButtonAtScreenPoint(double screenX, double screenY)
    {
        if (!IsVisible || !IsLoaded)
        {
            return false;
        }

        if (!TryResolveScreenBounds(out var bounds))
        {
            return false;
        }

        if (screenX < bounds.Left
            || screenX > bounds.Right
            || screenY < bounds.Top
            || screenY > bounds.Bottom)
        {
            return false;
        }

        var localPoint = PointFromScreen(new System.Windows.Point(screenX, screenY));
        var hit = InputHitTest(localPoint) as DependencyObject;
        if (hit == null)
        {
            hit = VisualTreeHelper.HitTest(this, localPoint)?.VisualHit;
        }

        var button = FindButtonBase(hit);
        if (button == null || !button.IsEnabled)
        {
            return false;
        }

        Activate();
        button.Focus();
        button.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent, button));
        return true;
    }

    private bool TryResolveScreenBounds(out System.Drawing.Rectangle bounds)
    {
        return WindowScreenBoundsResolver.TryResolve(this, out bounds, out _, out _);
    }

    private static System.Windows.Controls.Primitives.ButtonBase? FindButtonBase(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase button)
            {
                return button;
            }

            current = GetParent(current);
        }

        return null;
    }

    private bool IsOverlayWhiteboardSceneActive()
    {
        var overlay = _overlay;
        if (overlay == null)
        {
            return false;
        }

        return overlay.CurrentSessionState.Scene == UiSceneKind.Whiteboard;
    }

    private bool IsSessionCaptureWhiteboardActive()
    {
        var overlay = _overlay;
        if (overlay == null || !overlay.IsPhotoModeActive)
        {
            return false;
        }

        return RegionScreenCaptureWorkflow.IsSessionRegionCaptureFilePath(overlay.CurrentDocumentPath);
    }

    private void RefreshBoardButtonVisualState()
    {
        SetBoardButtonChecked(ToolbarBoardSelectionVisualPolicy.Resolve(
            _boardActive,
            IsOverlayWhiteboardSceneActive() || _overlay?.IsWhiteboardActive == true,
            IsSessionCaptureWhiteboardActive(),
            _directWhiteboardEntryArmed,
            _regionCapturePending));
    }

    private void SetBoardButtonChecked(bool isChecked)
    {
        _initializing = true;
        try
        {
            BoardButton.IsChecked = isChecked;
        }
        finally
        {
            _initializing = false;
        }
    }

    private void OpenQuickColorDialog(int index)
    {
        PrepareForNonBoardToolbarAction(exitWhiteboard: true);
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
            SelectToolMode(PaintToolMode.Brush, allowToggleOffCurrent: false);
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
        using var _ = FloatingTopmostDialogSuppressionState.Enter();
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
        button.ToolTip = $"颜色 {index + 1}：{GetQuickColorDisplayName(color)}。点按使用，再点/长按换色";
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

    private static string GetQuickColorDisplayName(MediaColor color)
    {
        if (color.R == Colors.Black.R && color.G == Colors.Black.G && color.B == Colors.Black.B)
        {
            return "黑色";
        }
        if (color.R == Colors.Red.R && color.G == Colors.Red.G && color.B == Colors.Red.B)
        {
            return "红色";
        }
        if (color.R == 0x1E && color.G == 0x90 && color.B == 0xFF)
        {
            return "蓝色";
        }
        if (color.R == 0x24 && color.G == 0xB4 && color.B == 0x7E)
        {
            return "绿色";
        }
        if (color.R == Colors.Yellow.R && color.G == Colors.Yellow.G && color.B == Colors.Yellow.B)
        {
            return "黄色";
        }
        if (color.R == Colors.Orange.R && color.G == Colors.Orange.G && color.B == Colors.Orange.B)
        {
            return "橙色";
        }
        if (color.R == 0x80 && color.G == 0x00 && color.B == 0x80)
        {
            return "紫色";
        }
        if (color.R == Colors.White.R && color.G == Colors.White.G && color.B == Colors.White.B)
        {
            return "白色";
        }

        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
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
        ResetPendingSecondTapState();
        PrepareForNonBoardToolbarAction(exitWhiteboard: false);
        SafeActionExecutionExecutor.TryExecute(
            () => SettingsRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: settings callback failed: {ex.Message}"));
    }


    private void PrepareForNonBoardToolbarAction(bool exitWhiteboard)
    {
        if (BoardActionsPopup != null)
        {
            BoardActionsPopup.IsOpen = false;
        }
        ClearDirectWhiteboardEntryArm();
        if (exitWhiteboard)
        {
            ExitWhiteboardForToolSwitchIfNeeded();
        }
    }

    private void ClearNonBoardSelectionVisualState()
    {
        _initializing = true;
        try
        {
            CursorButton.IsChecked = false;
            EraserButton.IsChecked = false;
            RegionEraseButton.IsChecked = false;
            ShapeButton.IsChecked = false;
            QuickColor1Button.IsChecked = false;
            QuickColor2Button.IsChecked = false;
            QuickColor3Button.IsChecked = false;
        }
        finally
        {
            _initializing = false;
        }
    }

    private void ExitWhiteboardForToolSwitchIfNeeded()
    {
        var whiteboardActive = _boardActive || IsOverlayWhiteboardSceneActive() || _overlay?.IsWhiteboardActive == true;
        if (!whiteboardActive)
        {
            return;
        }

        SetBoardActive(false);
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

    private void ResetToolSelectionBaselineForBoardInteraction()
    {
        _toolSelectionManager.Reset(PaintToolMode.Brush);
        if (_currentMode != PaintToolMode.Brush)
        {
            ApplyToolMode(PaintToolMode.Brush);
            return;
        }

        _overlay?.SetMode(PaintToolMode.Brush);
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
        ShapeButton.ToolTip = $"图形：当前{GetShapeDisplayName(type)}。点按使用，再点/长按选择";
    }

    private static string GetShapeDisplayName(PaintShapeType type)
    {
        return type switch
        {
            PaintShapeType.DashedLine => "虚线",
            PaintShapeType.Arrow => "箭头",
            PaintShapeType.DashedArrow => "虚线箭头",
            PaintShapeType.Rectangle => "空心矩形",
            PaintShapeType.RectangleFill => "实心矩形",
            PaintShapeType.Ellipse => "椭圆",
            PaintShapeType.Triangle => "三角形",
            _ => "直线"
        };
    }

    private void OnToolbarDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }
        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _toolbarDragging = true;
        _toolbarDragOffset = e.GetPosition(this);
        _toolbarDragScope?.Dispose();
        _toolbarDragScope = WindowDragOperationState.Begin();
        CaptureMouse();
        e.Handled = true;
    }

    private void OnToolbarTouchDragStart(object sender, TouchEventArgs e)
    {
        if (_toolbarTouchDragging)
        {
            return;
        }
        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _toolbarTouchDragging = true;
        _activeToolbarTouchDevice = e.TouchDevice;
        _toolbarTouchDragOffset = e.GetTouchPoint(this).Position;
        _toolbarDragScope?.Dispose();
        _toolbarDragScope = WindowDragOperationState.Begin();
        CaptureTouch(_activeToolbarTouchDevice);
        e.Handled = true;
    }

    private void OnToolbarDragMove(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarPointerMove();

        if (!_toolbarDragging)
        {
            return;
        }
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndToolbarDragCore();
            return;
        }

        var screen = PointToScreen(e.GetPosition(this));
        MoveToolbarWithinVirtualScreen(screen.X - _toolbarDragOffset.X, screen.Y - _toolbarDragOffset.Y);
    }

    private void TryResumeRegionCaptureIfPointerOutsideToolbar()
    {
        var screenPoint = System.Windows.Forms.Cursor.Position;
        var decision = RegionCaptureResumeTriggerPolicy.Resolve(
            _resumeRegionCaptureArmed,
            IsVisible,
            IsLoaded,
            BoardActive,
            _overlay?.IsWhiteboardActive == true,
            IsPointInsideToolbar(screenPoint.X, screenPoint.Y));
        if (decision.ShouldClearDirectWhiteboardEntryArm)
        {
            ClearDirectWhiteboardEntryArm();
            return;
        }

        if (!decision.ShouldResumeRegionCapture)
        {
            return;
        }

        _resumeRegionCaptureArmed = false;
        SafeActionExecutionExecutor.TryExecute(
            () => RegionCaptureRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: region capture resume callback failed: {ex.Message}"));
    }

    private void OnToolbarDragEnd(object? sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        EndToolbarDragCore();
    }

    private void EndToolbarDragCore()
    {
        if (!_toolbarDragging)
        {
            return;
        }

        _toolbarDragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        _toolbarDragScope?.Dispose();
        _toolbarDragScope = null;
    }

    private void OnToolbarTouchDragMove(object? sender, TouchEventArgs e)
    {
        RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarPointerMove();

        if (!_toolbarTouchDragging || !ReferenceEquals(_activeToolbarTouchDevice, e.TouchDevice))
        {
            return;
        }

        var screen = PointToScreen(e.GetTouchPoint(this).Position);
        MoveToolbarWithinVirtualScreen(screen.X - _toolbarTouchDragOffset.X, screen.Y - _toolbarTouchDragOffset.Y);
        e.Handled = true;
    }

    private void OnToolbarTouchDragEnd(object? sender, TouchEventArgs e)
    {
        if (!_toolbarTouchDragging || !ReferenceEquals(_activeToolbarTouchDevice, e.TouchDevice))
        {
            return;
        }

        EndToolbarTouchDragCore();
        e.Handled = true;
    }

    private void OnToolbarTouchLostCapture(object? sender, TouchEventArgs e)
    {
        if (_toolbarTouchDragging && ReferenceEquals(_activeToolbarTouchDevice, e.TouchDevice))
        {
            EndToolbarTouchDragCore();
        }
    }

    private void EndToolbarTouchDragCore()
    {
        if (!_toolbarTouchDragging)
        {
            return;
        }

        _toolbarTouchDragging = false;
        if (_activeToolbarTouchDevice != null)
        {
            ReleaseTouchCapture(_activeToolbarTouchDevice);
            _activeToolbarTouchDevice = null;
        }

        _toolbarDragScope?.Dispose();
        _toolbarDragScope = null;
    }

    private void MoveToolbarWithinVirtualScreen(double proposedLeft, double proposedTop)
    {
        var clampedLeft = Math.Max(
            SystemParameters.VirtualScreenLeft,
            Math.Min(proposedLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
        var clampedTop = Math.Max(
            SystemParameters.VirtualScreenTop,
            Math.Min(proposedTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));

        Left = clampedLeft;
        Top = clampedTop;
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

    private void RecordInteractionScreenPoint(System.Windows.Point point)
    {
        var screenPoint = PointToScreen(point);
        _lastInteractionScreenPoint = new System.Drawing.Point(
            (int)Math.Round(screenPoint.X),
            (int)Math.Round(screenPoint.Y));
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
            tryForwardPresentationKey: overlay.ForwardKeyboardToPresentation);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var overlay = _overlay;
        if (overlay == null)
        {
            return;
        }

        var handled = AuxWindowWheelRoutingHandler.TryHandle(
            delta: e.Delta,
            overlayVisible: overlay.IsVisible,
            canRoutePresentationInput: overlay.CanRoutePresentationInputFromAuxWindow(),
            tryForwardPresentationWheel: overlay.ForwardWheelToPresentation);
        if (handled)
        {
            e.Handled = true;
        }
    }

}
