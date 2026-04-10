using System.Diagnostics;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Windowing;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnPhotoTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        e.Handled = true;
        var plan = PhotoTitleBarDragZOrderPolicy.Resolve(
            _photoModeActive,
            _photoFullscreen,
            e.ChangedButton);
        if (!plan.CanDrag)
        {
            return;
        }

        if (plan.RequestZOrderBeforeDrag)
        {
            SafeActionExecutionExecutor.TryExecute(
                () => FloatingZOrderRequested?.Invoke(new FloatingZOrderRequest(plan.ForceAfterDrag)),
                ex => Debug.WriteLine($"[FloatingZOrderRequested] drag-before callback failed: {ex.GetType().Name} - {ex.Message}"));
        }

        PaintActionInvoker.TryInvoke(DragMove);

        if (plan.RequestZOrderAfterDrag)
        {
            SafeActionExecutionExecutor.TryExecute(
                () => FloatingZOrderRequested?.Invoke(new FloatingZOrderRequest(plan.ForceAfterDrag)),
                ex => Debug.WriteLine($"[FloatingZOrderRequested] drag-after callback failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    private void ShowPhotoContextMenu(WpfPoint position)
    {
        if (!IsPhotoFullscreenActive || _mode != PaintToolMode.Cursor)
        {
            return;
        }
        var menu = new System.Windows.Controls.ContextMenu();
        var closeItem = new System.Windows.Controls.MenuItem
        {
            Header = "关闭"
        };
        closeItem.Click += OnPhotoContextMenuCloseClick;
        menu.Items.Add(closeItem);
        menu.PlacementTarget = OverlayRoot;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void OnPhotoContextMenuCloseClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem item)
        {
            item.Click -= OnPhotoContextMenuCloseClick;
        }

        ExecutePhotoClose();
    }

    private void OnPhotoCloseClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        ExecutePhotoClose();
        if (e.RoutedEvent != null)
        {
            e.Handled = true;
        }
    }

    private void OnPhotoPrevClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        HandlePhotoNavigationRequest(-1);
    }

    private void OnPhotoNextClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        HandlePhotoNavigationRequest(1);
    }

    private void HandlePhotoNavigationRequest(int direction)
    {
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        PhotoNavigationDiagnostics.Log(
            "Overlay.Nav",
            $"dir={direction}, isPdf={_photoDocumentIsPdf}, page={_currentPageIndex}, seqIndex={_photoSequenceIndex}, crossPage={IsCrossPageDisplaySettingEnabled()}");
        if (TryHandleInDocumentNavigation(direction))
        {
            PhotoNavigationDiagnostics.Log("Overlay.Nav", "handled in-document");
            return;
        }
        RequestPhotoFileNavigation(direction);
    }

    private bool TryHandleInDocumentNavigation(int direction)
    {
        // 先按可视版面切换（上一版/下一版）
        if (TryNavigatePhotoEdition(direction))
        {
            return true;
        }
        // PDF 仅允许在当前文档内导航，禁止触发文件间切换。
        if (_photoDocumentIsPdf)
        {
            TryNavigatePdf(direction);
            return true;
        }
        return false;
    }

    private void RequestPhotoFileNavigation(int direction)
    {
        if (_photoDocumentIsPdf)
        {
            PhotoNavigationDiagnostics.Log("Overlay.Nav", "skip file-nav because current is pdf");
            return;
        }
        PhotoNavigationDiagnostics.Log("Overlay.Nav", "request file-nav to MainWindow");
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoNavigationRequested?.Invoke(direction),
            ex => Debug.WriteLine($"[PhotoNavigationRequested] callback failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private bool TryNavigatePhotoEdition(int direction)
    {
        if (!_photoModeActive || direction == 0)
        {
            return false;
        }
        if (_rememberPhotoTransform && TryStepPhotoViewport(direction))
        {
            return true;
        }
        if (TryNavigatePdf(direction))
        {
            return true;
        }
        return false;
    }
}
