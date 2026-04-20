using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintToolbarWindow
{
    private void OnQuickColorPointerDown(object sender, MouseButtonEventArgs e)
    {
        PrepareQuickColorSecondTap(sender);
    }

    private void OnQuickColorTouchDown(object sender, TouchEventArgs e)
    {
        PrepareQuickColorSecondTap(sender);
    }

    private void PrepareQuickColorSecondTap(object sender)
    {
        if (sender is not ToggleButton button)
        {
            return;
        }

        var index = ResolveQuickColorIndex(button.Tag);
        if (!index.HasValue)
        {
            return;
        }

        _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
            alreadySelected: button.IsChecked == true,
            supportsSecondaryAction: true,
            requestedTarget: ToolbarSecondTapTarget.QuickColor);
        _pendingQuickColorIndex = _pendingSecondTapTarget == ToolbarSecondTapTarget.QuickColor
            ? index.Value
            : null;
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
            ResetPendingSecondTapState();
            return;
        }

        if (_pendingSecondTapTarget == ToolbarSecondTapTarget.QuickColor && _pendingQuickColorIndex == index.Value)
        {
            button.IsChecked = true;
            OpenQuickColorDialog(index.Value);
            ResetPendingSecondTapState();
            return;
        }

        ResetPendingSecondTapState();
        ApplyQuickColorSelection(index.Value);
    }

    private void ApplyQuickColorSelection(int index)
    {
        PrepareForNonBoardToolbarAction(exitWhiteboard: true);
        
        var shouldResetShape = _shapeType != PaintShapeType.None;
        var selectedColor = _quickColors[index];
        
        // 更新颜色选择状态
        UpdateQuickColorSelection(selectedColor);
        
        // 始终同步回画笔模式，避免工具高亮状态残留
        SelectToolMode(PaintToolMode.Brush, allowToggleOffCurrent: false);
        
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

    private void OnShapePointerDown(object sender, MouseButtonEventArgs e)
    {
        PrepareShapeSecondTap();
    }

    private void OnShapeTouchDown(object sender, TouchEventArgs e)
    {
        PrepareShapeSecondTap();
    }

    private void PrepareShapeSecondTap()
    {
        _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
            alreadySelected: ShapeButton.IsChecked == true,
            supportsSecondaryAction: true,
            requestedTarget: ToolbarSecondTapTarget.Shape);
        _pendingQuickColorIndex = null;
    }

    private void OnShapeButtonClick(object sender, RoutedEventArgs e)
    {
        if (_pendingSecondTapTarget == ToolbarSecondTapTarget.Shape)
        {
            ShapeButton.IsChecked = true;
            OpenShapeMenu();
            ResetPendingSecondTapState();
            return;
        }

        ResetPendingSecondTapState();
        PrepareForNonBoardToolbarAction(exitWhiteboard: true);
        var shapeType = ResolveEffectiveShapeType();
        ApplyShapeType(shapeType);
        SelectToolMode(PaintToolMode.Shape, allowToggleOffCurrent: true);
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

    private void OnBoardPointerDown(object sender, MouseButtonEventArgs e)
    {
        PrepareBoardSecondTap();
    }

    private void OnBoardTouchDown(object sender, TouchEventArgs e)
    {
        PrepareBoardSecondTap();
    }

    private void PrepareBoardSecondTap()
    {
        _pendingSecondTapTarget = ToolbarSecondTapIntentPolicy.Resolve(
            alreadySelected: BoardButton.IsChecked == true || _regionCapturePending,
            supportsSecondaryAction: true,
            requestedTarget: ToolbarSecondTapTarget.Board);
        _pendingQuickColorIndex = null;
    }

    private void OnBoardClick(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            ResetPendingSecondTapState();
            return;
        }

        if (_regionCapturePending || _directWhiteboardEntryArmed || _resumeRegionCaptureArmed)
        {
            RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarHandledPress();
        }

        if (IsSessionCaptureWhiteboardActive())
        {
            ClearDirectWhiteboardEntryArm();
            _regionCapturePending = false;
            SetBoardActive(false);
            _overlay?.ExitPhotoMode();
            RefreshBoardButtonVisualState();
            ShowBoardHint("已退出白板");
            return;
        }

        var whiteboardActive = _boardActive || IsOverlayWhiteboardSceneActive() || _overlay?.IsWhiteboardActive == true;
        if (whiteboardActive)
        {
            ClearDirectWhiteboardEntryArm();
            _regionCapturePending = false;
            SetBoardActive(false);
            ShowBoardHint("已退出白板");
            return;
        }

        var shouldEnterWhiteboardBySecondTap = _pendingSecondTapTarget == ToolbarSecondTapTarget.Board;
        ResetPendingSecondTapState();
        if (shouldEnterWhiteboardBySecondTap)
        {
            _lastBoardPrimaryAction = BoardPrimaryAction.EnterWhiteboard;
            EnterWhiteboardAction();
            return;
        }

        if ((_directWhiteboardEntryArmed || _resumeRegionCaptureArmed || _regionCapturePending)
            && _overlay?.IsPhotoModeActive != true)
        {
            _lastBoardPrimaryAction = BoardPrimaryAction.EnterWhiteboard;
            EnterWhiteboardAction();
            return;
        }

        if (_overlay?.IsPhotoModeActive == true)
        {
            _lastBoardPrimaryAction = BoardPrimaryAction.EnterWhiteboard;
            EnterWhiteboardAction();
            return;
        }

        if (_lastBoardPrimaryAction != BoardPrimaryAction.CaptureRegion)
        {
            _lastBoardPrimaryAction = BoardPrimaryAction.CaptureRegion;
        }
        BeginRegionCaptureAction();
    }

    private void BeginRegionCaptureAction()
    {
        _lastBoardPrimaryAction = BoardPrimaryAction.CaptureRegion;
        ResetToolSelectionBaselineForBoardInteraction();
        ClearNonBoardSelectionVisualState();
        _regionCapturePending = true;
        ShowBoardHint("请框选截图区域");
        RefreshBoardButtonVisualState();
        SafeActionExecutionExecutor.TryExecute(
            () => RegionCaptureRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: region capture callback failed: {ex.Message}"));
    }

    private void EnterWhiteboardAction()
    {
        _lastBoardPrimaryAction = BoardPrimaryAction.EnterWhiteboard;
        ResetToolSelectionBaselineForBoardInteraction();
        ClearNonBoardSelectionVisualState();
        ClearDirectWhiteboardEntryArm();
        _regionCapturePending = false;
        SetBoardActive(true);
        ShowBoardHint("已进入白板");
    }

    private void OnBoardCaptureActionClick(object sender, RoutedEventArgs e)
    {
        ResetPendingSecondTapState();
        _lastBoardPrimaryAction = BoardPrimaryAction.CaptureRegion;
        BoardActionsPopup.IsOpen = false;
        BeginRegionCaptureAction();
    }

    private void OnBoardWhiteboardActionClick(object sender, RoutedEventArgs e)
    {
        ResetPendingSecondTapState();
        _lastBoardPrimaryAction = BoardPrimaryAction.EnterWhiteboard;
        BoardActionsPopup.IsOpen = false;
        EnterWhiteboardAction();
    }

    private void OnBoardColorActionClick(object sender, RoutedEventArgs e)
    {
        ResetPendingSecondTapState();
        BoardActionsPopup.IsOpen = false;
        OpenBoardColorDialog();
    }

    private void ResetPendingSecondTapState()
    {
        _pendingSecondTapTarget = ToolbarSecondTapTarget.None;
        _pendingQuickColorIndex = null;
    }

}
