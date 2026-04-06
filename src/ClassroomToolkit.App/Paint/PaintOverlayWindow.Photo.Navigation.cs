using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Shell;
using System.Windows.Interop;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.App.Paint.Brushes;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    public void SetPhotoSequence(IReadOnlyList<string> paths, int currentIndex)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var normalized = PhotoCrossPageSequencePolicy.Normalize(paths, currentIndex);
        _photoSequencePaths = normalized.Sequence.ToList();
        _photoSequenceIndex = normalized.CurrentIndex;
        ClearNeighborImageCache();
    }

    public bool IsPhotoModeActive => _photoModeActive;
    public bool IsPhotoFullscreenActive => _photoModeActive && _photoFullscreen;

    public void EnterPhotoMode(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }
        _photoUnboundedInkCanvasEnabled = false;
        ResetCrossPageReplayState();
        _crossPageUpdateDeferredByInkInput = false;
        RecoverInkWalForDirectory(sourcePath);
        _foregroundPhotoActive = false;
        var reentryPlan = PhotoOverlayReentryPolicy.Resolve(
            windowMinimized: WindowState == WindowState.Minimized,
            photoModeActive: _photoModeActive,
            sameSourcePath: string.Equals(_currentDocumentPath, sourcePath, StringComparison.OrdinalIgnoreCase));
        WindowStateNormalizationExecutor.Apply(this, reentryPlan.NormalizeWindowState);
        if (reentryPlan.ReturnEarly)
        {
            OverlayFocusExecutionExecutor.Apply(
                this,
                reentryPlan.ActivateOverlay,
                shouldKeyboardFocus: false);
            return;
        }
        var wasFullscreen = _photoModeActive ? _photoFullscreen : true;
        var wasPresentationFullscreen = false;
        if (!_photoModeActive && (_presentationOptions.AllowOffice || _presentationOptions.AllowWps))
        {
            var target = _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                _presentationOptions.AllowWps,
                _presentationOptions.AllowOffice,
                _currentProcessId);
            wasPresentationFullscreen = IsFullscreenPresentationWindow(target);
        }
        if (_photoModeActive)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
        }
        else if (wasPresentationFullscreen || _presentationFullscreenActive)
        {
            _presentationFullscreenActive = false;
            ClearCurrentPresentationType();
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            ClearInkSurfaceState();
        }
        else if (!_photoModeActive && (_inkStrokes.Count > 0 || _hasDrawing))
        {
            ClearInkSurfaceState();
        }
        var isPdf = IsPdfFile(sourcePath);
        if (_photoModeActive && _photoDocumentIsPdf)
        {
            ClosePdfDocument();
        }
        _currentPageIndex = 1;
        var hadUserTransformDirty = _photoUserTransformDirty;
        _photoUserTransformDirty = false;
        EnsurePhotoTransformsWritable();
        var transformInitPlan = PhotoEnterTransformInitPolicy.Resolve(
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled(),
            rememberPhotoTransform: _rememberPhotoTransform,
            photoUnifiedTransformReady: _photoUnifiedTransformReady,
            hadUserTransformDirty: hadUserTransformDirty);
        if (transformInitPlan.ShouldApplyUnifiedTransform)
        {
            ApplyLastUnifiedPhotoTransform(markUserDirty: transformInitPlan.ShouldMarkUserDirtyAfterUnifiedApply);
            if (transformInitPlan.ShouldMarkUnifiedTransformReady)
            {
                _photoUnifiedTransformReady = true;
            }
        }
        else if (transformInitPlan.ShouldTryStoredTransform)
        {
            var initialKey = BuildPhotoModeCacheKey(sourcePath, _currentPageIndex, isPdf);
            if (!TryApplyStoredPhotoTransform(initialKey))
            {
                ApplyIdentityPhotoTransform();
            }
        }
        else if (transformInitPlan.ShouldResetIdentity)
        {
            ApplyIdentityPhotoTransform();
        }
        _photoModeActive = true;
        UpdatePhotoContentTransforms(enabled: true);
        _photoFullscreen = wasFullscreen;
        _photoRestoreFullscreenPending = false;
        _presentationFullscreenActive = false;
            ClearCurrentPresentationType();
        EnsureOverlayTopmost(enforceZOrder: false);
        _currentCourseDate = DateTime.Today;
        _currentDocumentName = IoPath.GetFileNameWithoutExtension(sourcePath);
        _currentDocumentPath = sourcePath;
        ResetCrossPageNormalizedWidth();
        _currentCacheScope = InkCacheScope.Photo;
        _currentCacheKey = BuildPhotoModeCacheKey(sourcePath, _currentPageIndex, isPdf);
        _photoDocumentIsPdf = isPdf;
        _globalInkHistory.Clear();
        SetPhotoWindowMode(_photoFullscreen);
        UpdateWpsNavHookState();
        UpdatePresentationFocusMonitor();
        HidePhotoLoadingOverlay();
        if (isPdf)
        {
            ClosePdfDocument();
            ShowPhotoLoadingOverlay("正在加载PDF...");
            StartPdfOpenAsync(sourcePath);
        }
        else
        {
            if (!TrySetPhotoBackground(sourcePath))
            {
                HidePhotoLoadingOverlay();
                ExitPhotoMode();
                return;
            }
        }
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoModeChanged?.Invoke(true),
            ex => Debug.WriteLine($"[PhotoModeChanged] enter callback failed: {ex.GetType().Name} - {ex.Message}"));
        if (PhotoTitleText != null)
        {
            PhotoTitleText.Text = IoPath.GetFileName(sourcePath);
        }
        SafeActionExecutionExecutor.TryExecute(
            () => InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate),
            ex => Debug.WriteLine($"[InkContextChanged] enter callback failed: {ex.GetType().Name} - {ex.Message}"));
        ResetInkHistory();
        LoadCurrentPageIfExists();
        if (IsCrossPageDisplayActive())
        {
            UpdateCrossPageDisplay();
        }
        DispatchSessionEvent(new EnterPhotoFullscreenEvent(MapPhotoSource(isPdf)));
    }

    public void ExitPhotoMode()
    {
        if (!_photoModeActive)
        {
            return;
        }
        ResetCrossPageReplayState();
        _crossPageUpdateDeferredByInkInput = false;
        Interlocked.Increment(ref _photoLoadToken);
        HidePhotoLoadingOverlay();
        _foregroundPhotoActive = false;
        FlushPhotoTransformSave();
        SaveCurrentPageOnNavigate(forceBackground: false);
        if (!InkPersistenceTogglePolicy.ShouldRetainRuntimeCacheOnPhotoExit(_inkSaveEnabled))
        {
            EvictRuntimeInkCacheForClosedPhotoSession();
        }
        PhotoBackground.Source = null;
        RefreshPhotoBackgroundVisibility();
        _photoPageScale.ScaleX = 1.0;
        _photoPageScale.ScaleY = 1.0;
        ResetCrossPageNormalizedWidth();
        ClearNeighborPages();
        ClosePdfDocument();
        if (!_rememberPhotoTransform)
        {
            EnsurePhotoTransformsWritable();
            ApplyIdentityPhotoTransform();
            _photoUserTransformDirty = false;
        }
        _photoModeActive = false;
        _photoUnboundedInkCanvasEnabled = false;
        _boardSuspendedPhotoCache = false;
        UpdatePhotoContentTransforms(enabled: false);
        _photoFullscreen = false;
        _photoRestoreFullscreenPending = false;
        _photoDocumentIsPdf = false;
        SetPhotoWindowMode(fullscreen: false);
        UpdateWpsNavHookState();
        UpdatePresentationFocusMonitor();
        UpdateOverlayHitTestVisibility();
        UpdateInputPassthrough();
        EnsureOverlayTopmost(enforceZOrder: false);
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoModeChanged?.Invoke(false),
            ex => Debug.WriteLine($"[PhotoModeChanged] exit callback failed: {ex.GetType().Name} - {ex.Message}"));
        _currentDocumentName = string.Empty;
        _currentDocumentPath = string.Empty;
        if (PhotoTitleText != null)
        {
            PhotoTitleText.Text = "图片应用";
        }
        _currentPageIndex = 1;
        _currentCacheScope = InkCacheScope.None;
        _currentCacheKey = string.Empty;
        _globalInkHistory.Clear();
        ClearInkSurfaceState();
        DispatchSessionEvent(new ExitPhotoFullscreenEvent());
    }

    public void EnsurePhotoWindowedMode()
    {
        if (!_photoModeActive || !_photoFullscreen)
        {
            return;
        }

        _photoFullscreen = false;
        SetPhotoWindowMode(fullscreen: false);
    }

    public void SetPhotoInkCanvasUnbounded(bool enabled)
    {
        if (_photoUnboundedInkCanvasEnabled == enabled)
        {
            return;
        }

        _photoUnboundedInkCanvasEnabled = enabled;
        UpdatePhotoInkClip();
        if (IsPhotoInkModeActive())
        {
            MarkInkTransformVersionDirty();
            RequestInkRedraw();
        }
    }

    private void EvictRuntimeInkCacheForClosedPhotoSession()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }

        var sourcePathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            _currentDocumentPath
        };
        if (!_photoDocumentIsPdf)
        {
            foreach (var sequencePath in _photoSequencePaths)
            {
                if (!string.IsNullOrWhiteSpace(sequencePath))
                {
                    sourcePathSet.Add(sequencePath);
                }
            }
        }

        foreach (var (cacheKey, _) in _photoCache.Snapshot())
        {
            if (!InkExportSnapshotBuilder.TryParseCacheKey(cacheKey, out var sourcePath, out var pageIndex))
            {
                continue;
            }
            if (!sourcePathSet.Contains(sourcePath))
            {
                continue;
            }

            _photoCache.Remove(cacheKey);
            ClearInkWalSnapshot(sourcePath, pageIndex);
        }
    }

    private void SetPhotoWindowMode(bool fullscreen)
    {
        var wasFullscreen = _photoFullscreen;
        _photoFullscreen = fullscreen;
        var fullscreenChanged = wasFullscreen != _photoFullscreen;
        if (_photoModeActive && wasFullscreen && !fullscreen)
        {
            SaveAndClearInkSurface();
        }
        PhotoControlLayer.Visibility = _photoModeActive
            ? Visibility.Visible
            : Visibility.Collapsed;
        PhotoWindowFrame.BorderThickness = _photoModeActive && !_photoFullscreen
            ? new Thickness(1)
            : new Thickness(0);
        if (_photoModeActive)
        {
            PhotoWindowFrame.Background = ResolvePhotoWindowBackgroundBrush();
        }
        else
        {
            PhotoWindowFrame.Background = MediaBrushes.Transparent;
        }
        if (_photoModeActive)
        {
            ResizeMode = _photoFullscreen ? ResizeMode.NoResize : ResizeMode.CanResize;
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.CaptionHeight = _photoFullscreen ? 0 : 28;
                chrome.ResizeBorderThickness = _photoFullscreen ? new Thickness(0) : new Thickness(6);
            }
            ApplyPhotoWindowBounds(_photoFullscreen);
            if (_photoFullscreen)
            {
                // Reassert topmost with force only when overlay is not topmost yet.
                var enforceTopmost = OverlayTopmostEnforcePolicy.ResolveForPhotoFullscreen(Topmost);
                EnsureOverlayTopmost(enforceZOrder: enforceTopmost);
                SchedulePhotoFullscreenBoundsEnforcement();
            }
        }
        else
        {
            ResizeMode = ResizeMode.NoResize;
            Interlocked.Increment(ref _photoFullscreenBoundsToken);
            var chrome = WindowChrome.GetWindowChrome(this);
            if (chrome != null)
            {
                chrome.CaptionHeight = 28;
                chrome.ResizeBorderThickness = new Thickness(6);
            }
            // Avoid leaving the overlay in Maximized(work-area) semantics.
            // A stale work-area maximize state can leak into the next photo fullscreen transition.
            RecoverOverlayFullscreenBounds();
        }
        ShowInTaskbar = _photoModeActive;
        UpdateOverlayHitTestVisibility();

        // 全屏模式下禁用标题栏的鼠标交互，防止拖动窗口
        if (PhotoTitleBar != null)
        {
            PhotoTitleBar.IsHitTestVisible = !fullscreen;
        }

        UpdateInputPassthrough();
        if (PhotoWindowModeZOrderRetouchPolicy.ShouldRequest(_photoModeActive, fullscreenChanged))
        {
            var forceEnforce = PhotoWindowModeZOrderRetouchPolicy.ShouldForceEnforce(_photoFullscreen);
            SafeActionExecutionExecutor.TryExecute(
                () => FloatingZOrderRequested?.Invoke(new FloatingZOrderRequest(forceEnforce)),
                ex => Debug.WriteLine($"[FloatingZOrderRequested] photo-window-mode callback failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    private MediaBrush ResolvePhotoWindowBackgroundBrush()
    {
        if (_boardColor.A == 0)
        {
            return MediaBrushes.White;
        }

        var color = System.Windows.Media.Color.FromArgb(255, _boardColor.R, _boardColor.G, _boardColor.B);
        var brush = new SolidColorBrush(color);
        if (brush.CanFreeze)
        {
            brush.Freeze();
        }

        return brush;
    }

    private void ApplyIdentityPhotoTransform()
    {
        _photoScale.ScaleX = 1.0;
        _photoScale.ScaleY = 1.0;
        _photoTranslate.X = 0;
        _photoTranslate.Y = 0;
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: true);
    }

    private void ApplyPhotoWindowBounds(bool fullscreen)
    {
        NormalizeOverlayWindowState(shouldNormalize: true);
        if (fullscreen)
        {
            var hwnd = ResolveOverlayWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                // Use device pixels to guarantee true monitor coverage (including taskbar area).
                var pxRect = GetCurrentMonitorRect(useWorkArea: false);
                var positioned = WindowPlacementExecutor.TryApplyBoundsNoActivateNoZOrder(
                    hwnd,
                    (int)Math.Round(pxRect.Left, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Top, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Width, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Height, MidpointRounding.AwayFromZero),
                    showWindow: true);
                if (positioned)
                {
                    return;
                }
            }
        }

        var rect = GetCurrentMonitorRectInDip(useWorkArea: !fullscreen);
        Left = rect.Left;
        Top = rect.Top;
        Width = rect.Width;
        Height = rect.Height;
    }

    private void UpdatePhotoContentTransforms(bool enabled)
    {
        var applyPhotoTransform = PhotoContentTransformPolicy.ShouldApplyPhotoTransform(
            enabledRequested: enabled,
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            transformAvailable: _photoContentTransform != null);
        RasterImage.RenderTransform = applyPhotoTransform
            ? _photoContentTransform!
            : _photoInkPanCompensation;
        if (!applyPhotoTransform)
        {
            ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: !IsPhotoInkModeActive());
            SyncPhotoInteractiveRefreshAnchor();
        }
        UpdatePhotoInkClip();
    }

    private void RefreshPhotoBackgroundVisibility()
    {
        PhotoBackground.Visibility = PhotoBackgroundVisibilityPolicy.Resolve(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            hasBackgroundSource: PhotoBackground.Source != null);
        UpdatePhotoInkClip();
    }

    private void UpdatePhotoInkClip()
    {
        Rect rasterClipBounds = Rect.Empty;
        Rect previewClipBounds = Rect.Empty;
        if (!_photoUnboundedInkCanvasEnabled && PhotoBackground.Source is BitmapSource bitmap)
        {
            var usePhotoTransform = ReferenceEquals(RasterImage.RenderTransform, _photoContentTransform);
            _ = TryBuildImageScreenRect(bitmap, _photoContentTransform, out var currentPageScreenRect);
            rasterClipBounds = PhotoInkCurrentPageClipPolicy.ResolveBounds(
                photoInkModeActive: IsPhotoInkModeActive(),
                crossPageDisplayActive: IsCrossPageDisplayActive(),
                photoFullscreenActive: IsPhotoFullscreenActive,
                usePhotoTransform: usePhotoTransform,
                currentPageScreenRect: currentPageScreenRect,
                pageWidthDip: GetBitmapDisplayWidthInDip(bitmap),
                pageHeightDip: GetBitmapDisplayHeightInDip(bitmap));
            previewClipBounds = PhotoInkPreviewClipPolicy.ResolveBounds(
                photoInkModeActive: IsPhotoInkModeActive(),
                crossPageDisplayActive: IsCrossPageDisplayActive(),
                photoFullscreenActive: IsPhotoFullscreenActive,
                usePhotoTransform: usePhotoTransform,
                currentPageScreenRect: currentPageScreenRect,
                pageWidthDip: GetBitmapDisplayWidthInDip(bitmap),
                pageHeightDip: GetBitmapDisplayHeightInDip(bitmap));
        }

        if (rasterClipBounds.IsEmpty)
        {
            RasterImage.Clip = null;
        }
        else if (RasterImage.Clip is RectangleGeometry rectangleClip)
        {
            rectangleClip.Rect = rasterClipBounds;
        }
        else
        {
            RasterImage.Clip = new RectangleGeometry(rasterClipBounds);
        }

        if (previewClipBounds.IsEmpty)
        {
            _visualHost.Clip = null;
        }
        else if (_visualHost.Clip is RectangleGeometry previewClip)
        {
            previewClip.Rect = previewClipBounds;
        }
        else
        {
            _visualHost.Clip = new RectangleGeometry(previewClipBounds);
        }
    }

    private IntPtr ResolveOverlayWindowHandle()
    {
        if (WindowHandleValidationInteropAdapter.IsValid(_hwnd))
        {
            return _hwnd;
        }
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            _hwnd = handle;
        }
        return handle;
    }

    private void SchedulePhotoFullscreenBoundsEnforcement()
    {
        if (!IsPhotoFullscreenActive)
        {
            return;
        }
        var token = Interlocked.Increment(ref _photoFullscreenBoundsToken);
        var lifecycleToken = _overlayLifecycleCancellation.Token;
        _ = SafeTaskRunner.Run(
            "PaintOverlayWindow.SchedulePhotoFullscreenBoundsEnforcement",
            async cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var delays = new[] { 30, 120, 280 };
                foreach (var delayMs in delays)
                {
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    var scheduled = TryBeginInvoke(() =>
                    {
                        if (token != _photoFullscreenBoundsToken || !IsPhotoFullscreenActive)
                        {
                            return;
                        }
                        ApplyPhotoWindowBounds(fullscreen: true);
                    }, DispatcherPriority.Render);
                    if (!scheduled && Dispatcher.CheckAccess())
                    {
                        if (token != _photoFullscreenBoundsToken || !IsPhotoFullscreenActive)
                        {
                            return;
                        }
                        ApplyPhotoWindowBounds(fullscreen: true);
                    }
                    else if (!scheduled)
                    {
                        Debug.WriteLine("[PhotoBounds] fullscreen-enforcement dispatch unavailable.");
                    }
                }
            },
            lifecycleToken,
            onError: ex => Debug.WriteLine(
                $"[PhotoBounds] fullscreen-enforcement failed: {ex.GetType().Name} - {ex.Message}"));
    }

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

    private void NavigateToPage(
        int newPageIndex,
        double newTranslateY,
        bool interactiveSwitch = false,
        BitmapSource? preloadedBitmap = null,
        bool deferCrossPageDisplayUpdate = false,
        int? previousPageIndexForInteractiveSwitch = null,
        BitmapSource? previousPageBitmapForInteractiveSwitch = null,
        bool clearPreservedNeighborInkFrames = false)
    {
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "navigate-enter",
                $"targetPage={newPageIndex} interactive={interactiveSwitch}");
        }
        var beforeCurrentPage = GetCurrentPageIndexForCrossPage();
        if (_photoDocumentIsPdf)
        {
            _currentPageIndex = newPageIndex;
            _currentCacheKey = BuildPdfCacheKey(_currentDocumentPath, _currentPageIndex);
            ResetInkHistory();
            _photoTranslate.Y = PhotoNavigationInkLoadTranslatePolicy.ResolveTranslateYBeforeLoad(
                _photoTranslate.Y,
                newTranslateY,
                pageChanged: beforeCurrentPage != newPageIndex,
                photoInkModeActive: IsPhotoInkModeActive(),
                crossPageDisplayActive: IsCrossPageDisplayActive());
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("navigate-load-start", "doc=pdf");
            }
            LoadCurrentPageIfExists(
                allowDiskFallback: !interactiveSwitch,
                preferInteractiveFastPath: interactiveSwitch);
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("navigate-load-end", "doc=pdf");
            }
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("navigate-render-start", "doc=pdf");
            }
            if (!RenderPdfPage(_currentPageIndex, interactiveSwitch, preloadedBitmap))
            {
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-render-failed", "doc=pdf");
                }
                return;
            }
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("navigate-render-end", "doc=pdf");
            }
        }
        else
        {
            if (newPageIndex <= 0 || newPageIndex > _photoSequencePaths.Count)
            {
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-skip", $"invalid-page={newPageIndex}");
                }
                return;
            }

            _photoSequenceIndex = newPageIndex - 1;
            if (_photoSequenceIndex >= 0 && _photoSequenceIndex < _photoSequencePaths.Count)
            {
                var newPath = _photoSequencePaths[_photoSequenceIndex];
                if (ClassroomToolkit.Application.UseCases.Photos.PhotoNavigationPlanner.ClassifyPath(newPath)
                    != ClassroomToolkit.Application.UseCases.Photos.PhotoFileType.Image)
                {
                    if (IsCrossPageFirstInputTraceActive())
                    {
                        MarkCrossPageFirstInputStage("navigate-skip", "target-not-image");
                    }
                    return;
                }
                _currentDocumentName = IoPath.GetFileNameWithoutExtension(newPath);
                _currentDocumentPath = newPath;
                _currentCacheKey = BuildPhotoCacheKey(newPath);
                ResetInkHistory();
                _photoTranslate.Y = PhotoNavigationInkLoadTranslatePolicy.ResolveTranslateYBeforeLoad(
                    _photoTranslate.Y,
                    newTranslateY,
                    pageChanged: beforeCurrentPage != newPageIndex,
                    photoInkModeActive: IsPhotoInkModeActive(),
                    crossPageDisplayActive: IsCrossPageDisplayActive());
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-image-switch-start");
                }
                var newBitmap = CrossPageSwitchBitmapResolver.ResolveForInteractiveSwitch(
                    interactiveSwitch,
                    preloadedBitmap,
                    () => GetPageBitmap(newPageIndex));
                if (newBitmap != null)
                {
                    // Put the target page bitmap in place first, so interactive ink fast-path
                    // can reuse neighbor-rendered bitmap without forcing a full redraw.
                    PhotoBackground.Source = newBitmap;
                    RefreshPhotoBackgroundVisibility();
                    UpdateCurrentPageWidthNormalization(newBitmap);
                }
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage(
                        "navigate-image-switch-end",
                        $"bitmap={(newBitmap != null ? "hit" : "miss")}");
                }
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-load-start", "doc=image");
                }
                LoadCurrentPageIfExists(
                    allowDiskFallback: !interactiveSwitch,
                    preferInteractiveFastPath: interactiveSwitch);
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-load-end", "doc=image");
                }
                if (PhotoTitleText != null)
                {
                    PhotoTitleText.Text = IoPath.GetFileName(newPath);
                }
            }
        }
        // Apply new position
        _photoTranslate.Y = newTranslateY;
        var viewportSyncAction = PhotoNavigationInkViewportSyncPolicy.ResolveAction(
            IsPhotoInkModeActive(),
            interactiveSwitch);
        if (viewportSyncAction == PhotoNavigationInkViewportSyncAction.UpdatePanCompensation)
        {
            UpdatePhotoInkPanCompensation();
        }
        else if (viewportSyncAction == PhotoNavigationInkViewportSyncAction.ResetPanCompensation)
        {
            // Page switch loads a page-specific raster; carrying previous-page pan compensation
            // can shift the entire current page ink layer out of view until a later redraw.
            ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: true);
        }
        else
        {
            UpdatePhotoInkClip();
        }

        var currentPageAfterNavigation = GetCurrentPageIndexForCrossPage();
        var pageChanged = beforeCurrentPage != currentPageAfterNavigation;
        var previousPageForNeighborSeed = previousPageIndexForInteractiveSwitch.GetValueOrDefault(beforeCurrentPage);
        var preservedPageForMutationClear = CrossPageMutationNeighborRetentionPolicy.ResolvePreservedPage(
            clearPreservedNeighborInkFrames,
            pageChanged,
            previousPageForNeighborSeed,
            currentPageAfterNavigation);
        if (CrossPageNavigationCurrentInkRefreshPolicy.ShouldRequest(
                pageChanged,
                interactiveSwitch,
                IsPhotoInkModeActive(),
                _mode))
        {
            RequestPhotoTransformInkRedraw();
        }

        if (IsCrossPageDisplayActive())
        {
            if (clearPreservedNeighborInkFrames && pageChanged)
            {
                // Prevent old/new page ink carryover across a mutation-triggered seam switch.
                // RenderNeighborPages may preserve previous slot ink frames for continuity,
                // but this switch requires hard page ownership boundaries.
                ClearNeighborInkVisuals(
                    clearSlotIdentity: true,
                    preservePageIndex: preservedPageForMutationClear);
                if (CrossPageMutationNeighborSeedPolicy.ShouldSeedPreviousPageAfterClear(
                        clearPreservedNeighborInkFrames,
                        pageChanged,
                        previousPageForNeighborSeed,
                        currentPageAfterNavigation))
                {
                    // Re-seed previous page neighbor frame immediately after clear to avoid
                    // old-page self-ink one-frame flash during fast seam crossing.
                    TrySeedNeighborFrameForInteractiveSwitch(
                        previousPageForNeighborSeed,
                        previousPageBitmapForInteractiveSwitch);
                }
            }
            if (interactiveSwitch)
            {
                // Keep pointer-down path lightweight: full boundary computation walks all pages and
                // may synchronously load uncached bitmaps from disk, which causes first-stroke stalls.
                if (pageChanged)
                {
                    // For brush cross-page input, keep the target page's previous neighbor slot
                    // visible until the next formal cross-page refresh. This avoids a one-switch
                    // blank current page when the current raster has not yet been rehydrated.
                    if (CrossPageCurrentPageSeedSlotHidePolicy.ShouldHide(_mode))
                    {
                        HideNeighborSlotForPage(GetCurrentPageIndexForCrossPage());
                    }
                }
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("bounds-skip", "interactive-switch");
                }
            }
            else
            {
                ApplyCrossPageBoundaryLimits();
            }
        }
        else
        {
            // Single-page mode clamp
            var newBitmapSource = PhotoBackground.Source as BitmapSource;
            if (newBitmapSource != null)
            {
                var newPageHeight = GetScaledPageHeight(newBitmapSource);
                var minY = -(newPageHeight - OverlayRoot.ActualHeight * 0.1);
                var maxY = OverlayRoot.ActualHeight * 0.9;
                _photoTranslate.Y = Math.Clamp(_photoTranslate.Y, minY, maxY);
            }
        }
        SafeActionExecutionExecutor.TryExecute(
            () => InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate),
            ex => Debug.WriteLine($"[InkContextChanged] enter callback failed: {ex.GetType().Name} - {ex.Message}"));
        if (interactiveSwitch)
        {
            TrySeedNeighborFrameForInteractiveSwitch(
                previousPageForNeighborSeed,
                previousPageBitmapForInteractiveSwitch);

            var refreshMode = CrossPageInteractiveSwitchRefreshPolicy.Resolve(_mode, deferCrossPageDisplayUpdate);
            if (CrossPageDeferredRefreshPolicy.ShouldArmOnInteractiveSwitch(refreshMode))
            {
                _crossPageUpdateDeferredByInkInput = true;
            }
            else
            {
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("crosspage-update-request");
                }
                if (refreshMode == CrossPageInteractiveSwitchRefreshMode.ImmediateDirect)
                {
                    RequestCrossPageDisplayUpdate(CrossPageUpdateSources.NavigateInteractiveBrush);
                }
                else
                {
                    var scheduled = TryBeginInvoke(
                        () => RequestCrossPageDisplayUpdate(CrossPageUpdateSources.NavigateInteractive),
                        DispatcherPriority.Background);
                    if (!scheduled)
                    {
                        RequestCrossPageDisplayUpdate(CrossPageUpdateSources.NavigateInteractiveFallback);
                    }
                }
            }
        }
        else
        {
            UpdateCrossPageDisplay();
        }
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "navigate-exit",
                $"activePage={GetCurrentPageIndexForCrossPage()} bgVisible={PhotoBackground.Visibility}");
        }
    }

    private void SaveCurrentPageIfNeeded()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
    }

    private void SaveCurrentPageOnNavigate(
        bool forceBackground,
        bool persistToSidecar = true,
        bool finalizeActiveOperation = true)
    {
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "save-enter",
                $"force={forceBackground} persist={persistToSidecar} dirty={IsCurrentPageDirty()}");
        }
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-skip", "scope!=photo");
            }
            return;
        }

        var hadActiveInkOperation = IsInkOperationActive();
        if (finalizeActiveOperation && hadActiveInkOperation)
        {
            FinalizeActiveInkOperation();
        }
        // Interactive seam switching prefers async persistence, but when cache is disabled
        // we must persist synchronously here to avoid losing the finalized previous-page stroke.
        var shouldPersistToSidecar = persistToSidecar || (!_inkCacheEnabled && hadActiveInkOperation);

        if (!forceBackground && !IsCurrentPageDirty())
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-skip", "not-dirty");
            }
            return;
        }
        var cacheKey = _currentCacheKey;

        // Interactive cross-page switching should avoid heavy clone/hash work on pointer-down.
        // When no active operation exists and cache already has current page snapshot, rely on
        // existing cache + delayed autosave to keep UI input path lightweight.
        if (!persistToSidecar
            && !forceBackground
            && !hadActiveInkOperation
            && _inkCacheEnabled
            && !string.IsNullOrWhiteSpace(cacheKey)
            && _photoCache.TryGet(cacheKey, out _))
        {
            ScheduleSidecarAutoSave();
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-fast-return", "cache-hit + autosave");
            }
            return;
        }

        List<InkStrokeData> strokes;
        var reusedCachedSnapshot = false;

        if (!persistToSidecar
            && !forceBackground
            && !hadActiveInkOperation
            && _inkCacheEnabled
            && !string.IsNullOrWhiteSpace(cacheKey)
            && _photoCache.TryGet(cacheKey, out var cachedStrokes))
        {
            strokes = cachedStrokes;
            reusedCachedSnapshot = true;
        }
        else
        {
            strokes = CloneCommittedInkStrokes();
        }

        if (_inkCacheEnabled && !string.IsNullOrWhiteSpace(cacheKey))
        {
            if (strokes.Count == 0)
            {
                _photoCache.Remove(cacheKey);
            }
            else if (!reusedCachedSnapshot)
            {
                _photoCache.Set(cacheKey, strokes);
                System.Diagnostics.Debug.WriteLine($"[InkCache] Saved {strokes.Count} strokes for key={cacheKey}");
            }
        }
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "save-cache-updated",
                $"strokes={strokes.Count} reused={reusedCachedSnapshot}");
        }

        if (shouldPersistToSidecar)
        {
            // Method A: also persist to sidecar file on disk
            PersistInkToSidecar(strokes, _currentDocumentPath, _currentPageIndex);
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-sidecar-sync");
            }
        }
        else
        {
            // Interactive cross-page input should avoid blocking IO on pointer down.
            // Persist old page snapshot asynchronously to keep consistency without UI stalls.
            if (_inkPersistence != null && _inkSaveEnabled && !string.IsNullOrWhiteSpace(_currentDocumentPath))
            {
                string snapshotHash = string.Empty;
                if (reusedCachedSnapshot
                    && _inkDirtyPages.TryGetRuntimeState(_currentDocumentPath, _currentPageIndex, out _, out var knownHash, out var dirty)
                    && dirty
                    && !string.IsNullOrWhiteSpace(knownHash))
                {
                    snapshotHash = knownHash;
                }
                else
                {
                    snapshotHash = ComputeInkHash(strokes);
                }
                var snapshot = new SidecarPersistSnapshot(
                    _inkPersistence,
                    _currentDocumentPath,
                    _currentPageIndex,
                    strokes,
                    snapshotHash);
                QueueSidecarAutoSave(snapshot);
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("save-sidecar-queued");
                }
            }
            else
            {
                ScheduleSidecarAutoSave();
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("save-sidecar-timer");
                }
            }
        }
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("save-exit");
        }
    }

    private void TrySeedNeighborFrameForInteractiveSwitch(
        int previousPage,
        BitmapSource? previousPageBitmap)
    {
        if (!IsCrossPageDisplayActive() || previousPage <= 0)
        {
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        if (previousPage == currentPage)
        {
            return;
        }
        if (_neighborPagesCanvas == null || PhotoBackground.Source is not BitmapSource currentBitmap)
        {
            return;
        }

        var neighborBitmap = previousPageBitmap ?? GetNeighborPageBitmapForRender(previousPage, allowSynchronousResolve: true);
        if (neighborBitmap == null)
        {
            return;
        }
        if (!_photoDocumentIsPdf)
        {
            _neighborImageCache[previousPage] = neighborBitmap;
        }

        while (_neighborPageImages.Count < 1)
        {
            var img = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _neighborPageImages.Add(img);
            _neighborPagesCanvas.Children.Add(img);
        }
        while (_neighborInkImages.Count < 1)
        {
            var inkImg = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            _neighborInkImages.Add(inkImg);
            _neighborPagesCanvas.Children.Add(inkImg);
        }

        var pageUid = previousPage.ToString(CultureInfo.InvariantCulture);
        var slotIndex = ResolveNeighborSlotIndexForInteractiveSeed(pageUid);
        while (_neighborPageImages.Count <= slotIndex)
        {
            var img = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };
            _neighborPageImages.Add(img);
            _neighborPagesCanvas.Children.Add(img);
        }
        while (_neighborInkImages.Count <= slotIndex)
        {
            var inkImg = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            _neighborInkImages.Add(inkImg);
            _neighborPagesCanvas.Children.Add(inkImg);
        }
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var baseTop = CrossPageInputNavigation.ComputePageOffset(
            currentPage,
            previousPage,
            page => GetScaledHeightForPage(page, normalizedWidthDip));
        var pageScaleRatio = 1.0;
        if (!_photoDocumentIsPdf && normalizedWidthDip > 0)
        {
            var pageWidth = GetBitmapDisplayWidthInDip(neighborBitmap);
            if (pageWidth > 0)
            {
                pageScaleRatio = normalizedWidthDip / pageWidth;
            }
        }

        var pageImg = _neighborPageImages[slotIndex];
        if (CrossPageFrameSourceAssignmentPolicy.ShouldAssign(pageImg.Source, neighborBitmap))
        {
            pageImg.Source = neighborBitmap;
        }
        pageImg.Visibility = Visibility.Visible;
        pageImg.Uid = pageUid;
        pageImg.Tag = baseTop;

        BitmapSource? inkBitmap = null;
        var cacheKey = BuildNeighborInkCacheKey(previousPage);
        if (inkBitmap == null)
        {
            inkBitmap = ResolveNeighborInkBitmap(previousPage, neighborBitmap, allowDeferredRender: false);
        }
        if (inkBitmap == null)
        {
            RequestDeferredNeighborInkRender(previousPage, neighborBitmap);
        }

        var inkImgFirst = _neighborInkImages[slotIndex];
        var targetInkBitmap = _inkShowEnabled ? inkBitmap : null;
        var currentInkOffsetDip = 0.0;
        var slotPageChanged = !string.Equals(inkImgFirst.Uid, pageUid, StringComparison.Ordinal);
        var shouldReplaceSeedFrame = CrossPageInteractiveSeedInkFramePolicy.ShouldReplaceFrame(
            _inkShowEnabled,
            hasCurrentFrame: inkImgFirst.Source != null,
            hasResolvedTargetFrame: targetInkBitmap != null,
            slotPageChanged: slotPageChanged);
        if (shouldReplaceSeedFrame
            && CrossPageFrameSourceAssignmentPolicy.ShouldAssign(
                inkImgFirst.Source,
                targetInkBitmap))
        {
            inkImgFirst.Source = targetInkBitmap;
            if (targetInkBitmap != null
                && TryGetNeighborInkCacheEntry(previousPage, out var cachedEntry)
                && ReferenceEquals(cachedEntry.Bitmap, targetInkBitmap))
            {
                currentInkOffsetDip = cachedEntry.HorizontalOffsetDip;
            }
        }
        inkImgFirst.Visibility = inkImgFirst.Source != null ? Visibility.Visible : Visibility.Collapsed;
        inkImgFirst.Uid = pageUid;
        SetNeighborInkSlotTag(inkImgFirst, baseTop, currentInkOffsetDip);
        ApplyNeighborSharedTransform(pageImg, inkImgFirst, pageScaleRatio, baseTop);

        _neighborPagesCanvas.Visibility = Visibility.Visible;
        _lastNeighborPagesNonEmptyUtc = GetCurrentUtcTimestamp();
        var visibleNeighborCount = _neighborPageImages.Count(img => img.Visibility == Visibility.Visible);
        var holdMs = CrossPageInteractiveHoldDurationPolicy.ResolveMs(visibleNeighborCount, _mode);
        if (holdMs > 0)
        {
            _interactiveSwitchPinnedNeighborPage = previousPage;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = GetCurrentUtcTimestamp().AddMilliseconds(holdMs);
        }
        else
        {
            _interactiveSwitchPinnedNeighborPage = 0;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
        }
        _inkDiagnostics?.OnCrossPageUpdateEvent(
            "seed",
            "navigate-interactive",
            $"page={previousPage} ink={(inkImgFirst.Source != null)} holdMs={holdMs}");
    }

    private int ResolveNeighborSlotIndexForInteractiveSeed(string pageUid)
    {
        for (var i = 0; i < _neighborPageImages.Count; i++)
        {
            if (string.Equals(_neighborPageImages[i].Uid, pageUid, StringComparison.Ordinal))
            {
                return i;
            }
        }

        for (var i = 0; i < _neighborPageImages.Count; i++)
        {
            if (_neighborPageImages[i].Visibility != Visibility.Visible)
            {
                return i;
            }
        }

        return _neighborPageImages.Count;
    }

    private void ApplyFullscreenBounds()
    {
        var rect = GetCurrentMonitorRect();
        Left = rect.Left;
        Top = rect.Top;
        Width = rect.Width;
        Height = rect.Height;
    }

    private Rect GetCurrentMonitorRect(bool useWorkArea = false)
    {
        var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var screen = System.Windows.Forms.Screen.FromHandle(handle);
        var r = useWorkArea ? screen.WorkingArea : screen.Bounds;
        return new Rect(r.X, r.Y, r.Width, r.Height);
    }
    
    private Rect GetCurrentMonitorRectInDip(bool useWorkArea = false)
    {
        var screenRect = GetCurrentMonitorRect(useWorkArea);
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            var matrix = source.CompositionTarget.TransformFromDevice;
            var topLeft = matrix.Transform(new WpfPoint(screenRect.Left, screenRect.Top));
            var bottomRight = matrix.Transform(new WpfPoint(screenRect.Right, screenRect.Bottom));
            return new Rect(topLeft, bottomRight);
        }
        return screenRect;
    }
}
