using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Globalization;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Photos;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool IsWithinPhotoControls(DependencyObject? source)
    {
        if (source == null)
        {
            return false;
        }
        // Any visual under the photo control layer should not trigger drawing/panning hit logic.
        return IsDescendantOf(source, PhotoControlLayer) ||
               IsDescendantOf(source, PhotoLoadingOverlay);
    }

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject? ancestor)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, ancestor))
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private static bool IsPhotoNavigationKey(Key key, out int direction)
    {
        direction = 0;
        if (key == Key.Right || key == Key.Down || key == Key.PageDown || key == Key.Space || key == Key.Enter)
        {
            direction = 1;
            return true;
        }
        if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            direction = -1;
            return true;
        }
        return false;
    }

    private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (IsBoardActive())
        {
            e.Handled = true;
            return;
        }
        if (_photoModeActive)
        {
            ZoomPhoto(e.Delta, e.GetPosition(OverlayRoot));
            e.Handled = true;
            return;
        }
        if (_mode != PaintToolMode.Cursor
            && _mode != PaintToolMode.Brush
            && _mode != PaintToolMode.Shape
            && _mode != PaintToolMode.Eraser
            && _mode != PaintToolMode.RegionErase)
        {
            return;
        }
        if (_mode == PaintToolMode.Cursor && _inputPassthroughEnabled)
        {
            return;
        }
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return;
        }
        if (_wpsNavHookActive && _wpsHookInterceptWheel)
        {
            var foregroundType = ResolveForegroundPresentationType();
            if (foregroundType == ClassroomToolkit.Interop.Presentation.PresentationType.Wps)
            {
                return;
            }
        }
        if (WpsHookRecentlyFired())
        {
            return;
        }
        var command = e.Delta < 0
            ? ClassroomToolkit.Services.Presentation.PresentationCommand.Next
            : ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        if (TrySendPresentationCommand(command))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_photoLoading)
        {
            e.Handled = true;
            return;
        }
        if (TryHandlePhotoKey(e.Key))
        {
            e.Handled = true;
            return;
        }
        if (IsBoardActive() || _photoModeActive)
        {
            return;
        }
        if (TryHandlePresentationKey(e.Key))
        {
            e.Handled = true;
        }
    }

    public bool TryHandlePhotoKey(Key key)
    {
        if (!_photoModeActive || IsBoardActive())
        {
            return false;
        }
        if (key == Key.Escape && _photoFullscreen)
        {
            _photoFullscreen = false;
            SetPhotoWindowMode(fullscreen: false);
            return true;
        }
        if (IsPhotoNavigationKey(key, out var direction))
        {
            PhotoNavigationDiagnostics.Log("Overlay.Key", $"key={key}, dir={direction}, isPdf={_photoDocumentIsPdf}");
            HandlePhotoNavigationRequest(direction);
            return true;
        }
        if (key == Key.Add || key == Key.OemPlus)
        {
            ZoomPhotoByFactor(PhotoKeyZoomStep);
            return true;
        }
        if (key == Key.Subtract || key == Key.OemMinus)
        {
            ZoomPhotoByFactor(1.0 / PhotoKeyZoomStep);
            return true;
        }
        return false;
    }


    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoLoading)
        {
            e.Handled = true;
            return;
        }
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }
        if (TryBeginPhotoPan(e))
        {
            return;
        }
        var position = e.GetPosition(OverlayRoot);
        HandlePointerDown(position);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_photoLoading)
        {
            HideEraserPreview();
            e.Handled = true;
            return;
        }
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            HideEraserPreview();
            return;
        }
        var position = e.GetPosition(OverlayRoot);
        _lastPointerPosition = position;
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed
                && e.RightButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                EndPhotoPan();
                e.Handled = true;
                return;
            }
            UpdatePhotoPan(position);
            e.Handled = true;
            return;
        }
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }
        HandlePointerMove(position);
        e.Handled = true;
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoLoading)
        {
            e.Handled = true;
            return;
        }
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            EndPhotoPan();
            e.Handled = true;
            return;
        }
        var position = e.GetPosition(OverlayRoot);
        HandlePointerUp(position);
        e.Handled = true;
    }

    private void OnRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoModeActive && _photoFullscreen && _mode == PaintToolMode.Cursor)
        {
            _photoRightClickPending = true;
            _photoRightClickStart = e.GetPosition(OverlayRoot);
        }
        if (TryBeginPhotoPan(e))
        {
            return;
        }
    }

    private void OnRightButtonMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoRightClickPending)
        {
            var point = e.GetPosition(OverlayRoot);
            var delta = point - _photoRightClickStart;
            if (delta.Length > 6)
            {
                _photoRightClickPending = false;
            }
        }
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            if (e.RightButton != System.Windows.Input.MouseButtonState.Pressed
                && e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
            {
                EndPhotoPan();
                e.Handled = true;
                return;
            }
            UpdatePhotoPan(e.GetPosition(OverlayRoot));
            e.Handled = true;
        }
    }

    private void OnOverlayLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            EndPhotoPan();
        }
        _photoRightClickPending = false;
    }

    private void OnOverlayMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HideEraserPreview();
    }

    private void OnRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject))
        {
            return;
        }
        if (_photoRightClickPending && _photoModeActive && _photoFullscreen && _mode == PaintToolMode.Cursor)
        {
            _photoRightClickPending = false;
            ShowPhotoContextMenu(e.GetPosition(OverlayRoot));
            e.Handled = true;
            return;
        }
        if (_photoPanning && _photoModeActive && _mode == PaintToolMode.Cursor)
        {
            EndPhotoPan();
            e.Handled = true;
        }
    }

    private void HandlePointerDown(WpfPoint position)
    {
        MarkInkInput();
        _lastPointerPosition = position;
        TrySwitchActiveImagePageForInput(position);
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("tool-dispatch", $"tool={_mode}");
        }
        // 设置正在绘图状态
        PaintModeManager.Instance.IsDrawing = true;
        
        if (_mode == PaintToolMode.RegionErase)
        {
            BeginRegionSelection(position);
            CapturePointerInput();
            return;
        }
        if (_mode == PaintToolMode.Eraser)
        {
            BeginEraser(position);
            CapturePointerInput();
            return;
        }
        if (_mode == PaintToolMode.Shape)
        {
            BeginShape(position);
            CapturePointerInput();
            return;
        }
        if (_mode == PaintToolMode.Brush)
        {
            BeginBrushStroke(position);
            CapturePointerInput();
        }
    }

    private void TrySwitchActiveImagePageForInput(WpfPoint position)
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled)
        {
            return;
        }
        if (_mode != PaintToolMode.Brush && _mode != PaintToolMode.Shape && _mode != PaintToolMode.Eraser && _mode != PaintToolMode.RegionErase)
        {
            return;
        }

        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            return;
        }

        var currentPage = GetCurrentPageIndexForCrossPage();
        if (TryResolveVisibleImagePageFromPointer(position, currentPage, out var hitPage, out var preloadedBitmap))
        {
            SwitchToImagePageForInput(currentPage, hitPage, currentBitmap, preloadedBitmap);
            return;
        }

        var targetPage = ResolveCrossPageTargetForInput(position.Y, currentPage, currentBitmap);
        SwitchToImagePageForInput(currentPage, targetPage, currentBitmap, preloadedBitmap: null);
    }

    private void SwitchToImagePageForInput(
        int currentPage,
        int targetPage,
        BitmapSource currentBitmap,
        BitmapSource? preloadedBitmap)
    {
        if (targetPage == currentPage || targetPage <= 0)
        {
            return;
        }

        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var offset = CrossPageInputNavigation.ComputePageOffset(
            currentPage,
            targetPage,
            page => GetScaledHeightForPage(page, normalizedWidthDip));

        BeginCrossPageFirstInputTrace(currentPage, targetPage);
        MarkCrossPageFirstInputStage("switch-resolved", $"offset={offset:F2}");

        // Cross-page pointer-down must stay lightweight to avoid first-stroke stalls.
        MarkCrossPageFirstInputStage("save-old-page-start");
        SaveCurrentPageOnNavigate(forceBackground: false, persistToSidecar: false);
        MarkCrossPageFirstInputStage("save-old-page-end");
        var previousInkBitmap = TrySnapshotCurrentInkSurfaceForNeighbor();
        MarkCrossPageFirstInputStage("navigate-start");
        NavigateToPage(
            targetPage,
            _photoTranslate.Y + offset,
            interactiveSwitch: true,
            preloadedBitmap: preloadedBitmap,
            deferCrossPageDisplayUpdate: true,
            previousPageIndexForInteractiveSwitch: currentPage,
            previousPageBitmapForInteractiveSwitch: currentBitmap,
            previousPageInkBitmapForInteractiveSwitch: previousInkBitmap);
        MarkCrossPageFirstInputStage("navigate-end", $"activePage={GetCurrentPageIndexForCrossPage()}");
    }

    private bool TryResolveVisibleImagePageFromPointer(
        WpfPoint pointer,
        int currentPage,
        out int pageIndex,
        out BitmapSource? resolvedBitmap)
    {
        pageIndex = currentPage;
        resolvedBitmap = null;
        if (!_crossPageDisplayEnabled)
        {
            return false;
        }

        if (PhotoBackground.Source is BitmapSource currentBitmap &&
            TryBuildImageScreenRect(currentBitmap, _photoContentTransform, out var currentRect) &&
            currentRect.Contains(pointer))
        {
            pageIndex = currentPage;
            resolvedBitmap = currentBitmap;
            return true;
        }

        foreach (var img in _neighborPageImages)
        {
            if (img.Visibility != Visibility.Visible || img.Source is not BitmapSource bitmap)
            {
                continue;
            }
            if (!int.TryParse(img.Uid, NumberStyles.Integer, CultureInfo.InvariantCulture, out var candidatePage))
            {
                continue;
            }
            if (!TryBuildImageScreenRect(bitmap, img.RenderTransform, out var rect))
            {
                continue;
            }
            if (rect.Contains(pointer))
            {
                pageIndex = candidatePage;
                resolvedBitmap = bitmap;
                return true;
            }
        }

        return false;
    }

    private bool TryBuildImageScreenRect(BitmapSource bitmap, Transform? transform, out Rect rect)
    {
        rect = Rect.Empty;
        if (bitmap == null || transform == null)
        {
            return false;
        }

        var width = GetBitmapDisplayWidthInDip(bitmap);
        var height = GetBitmapDisplayHeightInDip(bitmap);
        if (width <= 0.5 || height <= 0.5)
        {
            return false;
        }

        var logicalRect = new Rect(0, 0, width, height);
        rect = Rect.Transform(logicalRect, transform.Value);
        return !rect.IsEmpty;
    }

    private int ResolveCrossPageTargetForInput(double pointerY, int currentPage, BitmapSource currentBitmap)
    {
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return currentPage;
        }

        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var currentHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        var currentTop = _photoTranslate.Y;
        return CrossPageInputNavigation.ResolveTargetPage(
            pointerY,
            currentPage,
            totalPages,
            currentTop,
            currentHeight,
            page => GetScaledHeightForPage(page, normalizedWidthDip));
    }

    private void HandlePointerMove(WpfPoint position)
    {
        MarkInkInput();
        _lastPointerPosition = position;
        if (_mode == PaintToolMode.Brush)
        {
            UpdateBrushStroke(position);
            return;
        }
        if (_mode == PaintToolMode.Eraser)
        {
            UpdateEraser(position);
            return;
        }
        if (_mode == PaintToolMode.RegionErase)
        {
            UpdateRegionSelection(position);
            return;
        }
        if (_mode == PaintToolMode.Shape)
        {
            UpdateShapePreview(position);
        }
    }

    private void HandlePointerUp(WpfPoint position)
    {
        MarkInkInput();
        _lastPointerPosition = position;
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("tool-end", $"tool={_mode}");
        }
        if (_mode == PaintToolMode.Brush)
        {
            EndBrushStroke(position);
        }
        else if (_mode == PaintToolMode.Eraser)
        {
            EndEraser(position);
        }
        else if (_mode == PaintToolMode.RegionErase)
        {
            EndRegionSelection(position);
        }
        else if (_mode == PaintToolMode.Shape)
        {
            EndShape(position);
        }
        ReleasePointerInput();
        if (_photoModeActive && _crossPageDisplayEnabled)
        {
            _lastCrossPagePointerUpUtc = DateTime.UtcNow;
            System.Threading.Interlocked.Increment(ref _crossPagePointerUpSequence);
        }
        if (_crossPageUpdateDeferredByInkInput)
        {
            _crossPageUpdateDeferredByInkInput = false;
            if (_photoModeActive && _crossPageDisplayEnabled)
            {
                // Keep first-stroke path lightweight, but do not permanently skip the seam refresh.
                // A short delayed refresh after pointer-up restores neighbor page/ink visibility.
                _inkDiagnostics?.OnCrossPageUpdateEvent("defer", "pointer-up", "stable-recover-v3");
                ScheduleCrossPageDisplayUpdateAfterInputSettles(
                    source: "pointer-up",
                    singlePerPointerUp: true,
                    delayOverrideMs: 140);
            }
        }
        if (IsCrossPageFirstInputTraceActive())
        {
            EndCrossPageFirstInputTrace("pointer-up");
        }
        if (_pendingInkContextCheck)
        {
            _pendingInkContextCheck = false;
            _refreshOrchestrator.RequestRefresh("pointer-up");
        }
    }

    private void OnManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor || IsBoardActive())
        {
            if (IsBoardActive())
            {
                e.Handled = true;
            }
            return;
        }
        e.ManipulationContainer = OverlayRoot;
        e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
        e.Handled = true;
    }

    private void OnManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor || IsBoardActive())
        {
            if (IsBoardActive())
            {
                e.Handled = true;
            }
            return;
        }
        if (_photoPanning)
        {
            e.Handled = true;
            return;
        }
        EnsurePhotoTransformsWritable();
        var scale = e.DeltaManipulation.Scale;
        // Filter tiny touch/manipulation noise to avoid accidental zoom jumps while dragging.
        if (Math.Abs(scale.X - 1.0) > 0.02 || Math.Abs(scale.Y - 1.0) > 0.02)
        {
            var factor = (scale.X + scale.Y) / 2.0;
            ApplyPhotoScale(factor, e.ManipulationOrigin);
        }
        var translation = e.DeltaManipulation.Translation;
        if (Math.Abs(translation.X) > 0.01 || Math.Abs(translation.Y) > 0.01)
        {
            _photoTranslate.X += translation.X;
            _photoTranslate.Y += translation.Y;
            SchedulePhotoTransformSave(userAdjusted: true);
        }
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate("manipulation-delta");
        }
    }

    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        if (_photoLoading) return;
        if (!IsBoardActive() && _mode == PaintToolMode.Cursor) return;
        
        // 如果正在操作照片控件，忽略
        if (_photoModeActive && IsWithinPhotoControls(e.OriginalSource as DependencyObject)) return;

        var position = e.GetPosition(OverlayRoot);
        HandlePointerDown(position);
        e.Handled = true;
    }

    private void OnStylusMove(object sender, StylusEventArgs e)
    {
        if (_photoLoading) return;
        if (!IsBoardActive() && _mode == PaintToolMode.Cursor) return;

        if (_strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting)
        {
            var position = e.GetPosition(OverlayRoot);
            HandlePointerMove(position);
            e.Handled = true;
        }
    }

    private void OnStylusUp(object sender, StylusEventArgs e)
    {
        if (_photoLoading) return;
        if (!IsBoardActive() && _mode == PaintToolMode.Cursor) return;

        if (_strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting)
        {
            var position = e.GetPosition(OverlayRoot);
            HandlePointerUp(position);
            e.Handled = true;
        }
    }

    private void CapturePointerInput()
    {
        OverlayRoot.CaptureMouse();
        Stylus.Capture(OverlayRoot);
    }

    private void ReleasePointerInput()
    {
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
        if (OverlayRoot.IsStylusCaptured)
        {
            Stylus.Capture(null);
        }
    }

    private bool IsCrossPageFirstInputTraceActive()
    {
        return false;
    }

    private void BeginCrossPageFirstInputTrace(int fromPage, int toPage)
    {
    }

    private void MarkCrossPageFirstInputStage(string stage, string? details = null)
    {
    }

    private void EndCrossPageFirstInputTrace(string outcome)
    {
    }
}
