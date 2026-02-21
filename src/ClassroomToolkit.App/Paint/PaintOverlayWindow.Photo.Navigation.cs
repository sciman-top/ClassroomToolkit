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
        _photoSequencePaths = paths?.ToList() ?? new List<string>();
        _photoSequenceIndex = currentIndex;
        ClearNeighborImageCache();
    }

    public bool IsPhotoModeActive => _photoModeActive;

    public void EnterPhotoMode(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }
        RecoverInkWalForDirectory(sourcePath);
        _foregroundPhotoActive = false;
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }
        if (_photoModeActive && string.Equals(_currentDocumentPath, sourcePath, StringComparison.OrdinalIgnoreCase))
        {
            Activate();
            return;
        }
        var wasFullscreen = true;
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
            _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
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
        _photoUserTransformDirty = false;
        EnsurePhotoTransformsWritable();
        if (_crossPageDisplayEnabled)
        {
            if (_photoUnifiedTransformReady)
            {
                _photoScale.ScaleX = _lastPhotoScaleX;
                _photoScale.ScaleY = _lastPhotoScaleY;
                _photoTranslate.X = _lastPhotoTranslateX;
                _photoTranslate.Y = _lastPhotoTranslateY;
                _photoUserTransformDirty = true;
            }
            else if (_photoUserTransformDirty)
            {
                _photoScale.ScaleX = _lastPhotoScaleX;
                _photoScale.ScaleY = _lastPhotoScaleY;
                _photoTranslate.X = _lastPhotoTranslateX;
                _photoTranslate.Y = _lastPhotoTranslateY;
                _photoUnifiedTransformReady = true;
            }
            else
            {
                _photoScale.ScaleX = 1.0;
                _photoScale.ScaleY = 1.0;
                _photoTranslate.X = 0;
                _photoTranslate.Y = 0;
            }
        }
        else if (_rememberPhotoTransform)
        {
            var initialKey = BuildPhotoModeCacheKey(sourcePath, _currentPageIndex, isPdf);
            if (!TryApplyStoredPhotoTransform(initialKey))
            {
                _photoScale.ScaleX = 1.0;
                _photoScale.ScaleY = 1.0;
                _photoTranslate.X = 0;
                _photoTranslate.Y = 0;
            }
        }
        else
        {
            _photoScale.ScaleX = 1.0;
            _photoScale.ScaleY = 1.0;
            _photoTranslate.X = 0;
            _photoTranslate.Y = 0;
        }
        _photoModeActive = true;
        UpdatePhotoContentTransforms(enabled: true);
        _photoFullscreen = wasFullscreen;
        _photoRestoreFullscreenPending = false;
        _presentationFullscreenActive = false;
        _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
        Topmost = true;
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
        PhotoModeChanged?.Invoke(true);
        if (PhotoTitleText != null)
        {
            PhotoTitleText.Text = IoPath.GetFileName(sourcePath);
        }
        InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate);
        ResetInkHistory();
        LoadCurrentPageIfExists();
        if (_crossPageDisplayEnabled)
        {
            UpdateCrossPageDisplay();
        }
    }

    public void ExitPhotoMode()
    {
        if (!_photoModeActive)
        {
            return;
        }
        Interlocked.Increment(ref _photoLoadToken);
        HidePhotoLoadingOverlay();
        _foregroundPhotoActive = false;
        FlushPhotoTransformSave();
        SaveCurrentPageOnNavigate(forceBackground: false);
        PhotoBackground.Source = null;
        PhotoBackground.Visibility = Visibility.Collapsed;
        _photoPageScale.ScaleX = 1.0;
        _photoPageScale.ScaleY = 1.0;
        ResetCrossPageNormalizedWidth();
        ClearNeighborPages();
        ClosePdfDocument();
        if (!_rememberPhotoTransform)
        {
            EnsurePhotoTransformsWritable();
            _photoScale.ScaleX = 1.0;
            _photoScale.ScaleY = 1.0;
            _photoTranslate.X = 0;
            _photoTranslate.Y = 0;
            _photoUserTransformDirty = false;
        }
        _photoModeActive = false;
        UpdatePhotoContentTransforms(enabled: false);
        _photoFullscreen = false;
        _photoRestoreFullscreenPending = false;
        _photoDocumentIsPdf = false;
        SetPhotoWindowMode(fullscreen: false);
        UpdateWpsNavHookState();
        UpdatePresentationFocusMonitor();
        OverlayRoot.IsHitTestVisible = _mode != PaintToolMode.Cursor || _photoModeActive;
        UpdateInputPassthrough();
        Topmost = true;
        PhotoModeChanged?.Invoke(false);
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
    }

    private void SetPhotoWindowMode(bool fullscreen)
    {
        var wasFullscreen = _photoFullscreen;
        _photoFullscreen = fullscreen;
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
            PhotoWindowFrame.Background = TryFindResource("Brush_Background") as MediaBrush ?? MediaBrushes.White;
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
                // Refresh topmost ordering to stay above appbar/taskbar after repeated transitions.
                Topmost = false;
                Topmost = true;
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
            WindowState = WindowState.Normal;
            ApplyFullscreenBounds();
            TryBeginInvoke(ApplyFullscreenBounds, DispatcherPriority.Background);
        }
        ShowInTaskbar = _photoModeActive;
        OverlayRoot.IsHitTestVisible = _mode != PaintToolMode.Cursor || _photoModeActive;

        // 全屏模式下禁用标题栏的鼠标交互，防止拖动窗口
        if (PhotoTitleBar != null)
        {
            PhotoTitleBar.IsHitTestVisible = !fullscreen;
        }

        UpdateInputPassthrough();
    }

    private void ApplyPhotoWindowBounds(bool fullscreen)
    {
        WindowState = WindowState.Normal;
        if (fullscreen)
        {
            var hwnd = ResolveOverlayWindowHandle();
            if (hwnd != IntPtr.Zero)
            {
                // Use device pixels to guarantee true monitor coverage (including taskbar area).
                var pxRect = GetCurrentMonitorRect(useWorkArea: false);
                var positioned = ClassroomToolkit.Interop.NativeMethods.SetWindowPos(
                    hwnd,
                    IntPtr.Zero,
                    (int)Math.Round(pxRect.Left, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Top, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Width, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Height, MidpointRounding.AwayFromZero),
                    SwpNoActivate
                    | SwpShowWindow
                    | SwpNoZorder
                    | ClassroomToolkit.Interop.NativeMethods.SwpFrameChanged
                    | ClassroomToolkit.Interop.NativeMethods.SwpNoOwnerZOrder);
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
        if (_photoContentTransform == null)
        {
            RasterImage.RenderTransform = Transform.Identity;
            return;
        }
        RasterImage.RenderTransform = enabled ? _photoContentTransform : Transform.Identity;
    }

    private IntPtr ResolveOverlayWindowHandle()
    {
        if (_hwnd != IntPtr.Zero && ClassroomToolkit.Interop.NativeMethods.IsWindow(_hwnd))
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
        if (!_photoModeActive || !_photoFullscreen)
        {
            return;
        }
        var token = Interlocked.Increment(ref _photoFullscreenBoundsToken);
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            var delays = new[] { 30, 120, 280 };
            foreach (var delayMs in delays)
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(delayMs).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
                TryBeginInvoke(() =>
                {
                    if (token != _photoFullscreenBoundsToken || !_photoModeActive || !_photoFullscreen)
                    {
                        return;
                    }
                    ApplyPhotoWindowBounds(fullscreen: true);
                }, DispatcherPriority.Render);
            }
        });
    }

    private void OnPhotoTitleBarDrag(object sender, MouseButtonEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        e.Handled = true;
        // 全屏模式下不允许拖动窗口
        if (_photoFullscreen)
        {
            return;
        }
        if (e.ChangedButton == MouseButton.Left)
        {
            FloatingZOrderRequested?.Invoke();
            try
            {
                DragMove();
            }
            catch
            {
                // Ignore drag exceptions.
            }
            FloatingZOrderRequested?.Invoke();
        }
    }

    private void ShowPhotoContextMenu(WpfPoint position)
    {
        if (!_photoModeActive || !_photoFullscreen || _mode != PaintToolMode.Cursor)
        {
            return;
        }
        var menu = new ContextMenu();
        var closeItem = new MenuItem
        {
            Header = "关闭"
        };
        closeItem.Click += (_, _) => ExecutePhotoClose();
        menu.Items.Add(closeItem);
        menu.PlacementTarget = OverlayRoot;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
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
        PhotoNavigationDiagnostics.Log(
            "Overlay.Nav",
            $"dir={direction}, isPdf={_photoDocumentIsPdf}, page={_currentPageIndex}, seqIndex={_photoSequenceIndex}, crossPage={_crossPageDisplayEnabled}");
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
        PhotoNavigationRequested?.Invoke(direction);
    }

    private bool TryNavigatePhotoEdition(int direction)
    {
        if (!_photoModeActive || direction == 0)
        {
            return false;
        }
        if (TryStepPhotoViewport(direction))
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
        BitmapSource? previousPageInkBitmapForInteractiveSwitch = null)
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
            _photoSequenceIndex = newPageIndex - 1;
            if (_photoSequenceIndex >= 0 && _photoSequenceIndex < _photoSequencePaths.Count)
            {
                var newPath = _photoSequencePaths[_photoSequenceIndex];
                _currentDocumentName = IoPath.GetFileNameWithoutExtension(newPath);
                _currentDocumentPath = newPath;
                _currentCacheKey = BuildPhotoCacheKey(newPath);
                ResetInkHistory();
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
                    PhotoBackground.Visibility = Visibility.Visible;
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

        if (_crossPageDisplayEnabled)
        {
            if (interactiveSwitch)
            {
                // Keep pointer-down path lightweight: full boundary computation walks all pages and
                // may synchronously load uncached bitmaps from disk, which causes first-stroke stalls.
                if (beforeCurrentPage != GetCurrentPageIndexForCrossPage())
                {
                    UpdateNeighborTransformsForPan();
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
        InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate);
        if (interactiveSwitch)
        {
            if (deferCrossPageDisplayUpdate)
            {
                var previousPage = previousPageIndexForInteractiveSwitch.GetValueOrDefault(beforeCurrentPage);
                TrySeedNeighborFrameForInteractiveSwitch(
                    previousPage,
                    previousPageBitmapForInteractiveSwitch,
                    previousPageInkBitmapForInteractiveSwitch);
                _crossPageUpdateDeferredByInkInput = true;
            }
            else
            {
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("crosspage-update-request");
                }
                var scheduled = TryBeginInvoke(() => RequestCrossPageDisplayUpdate("navigate-interactive"), DispatcherPriority.Background);
                if (!scheduled)
                {
                    RequestCrossPageDisplayUpdate("navigate-interactive-fallback");
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

    private void SaveCurrentPageOnNavigate(bool forceBackground, bool persistToSidecar = true)
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
        if (!forceBackground && !IsCurrentPageDirty())
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-skip", "not-dirty");
            }
            return;
        }
        var cacheKey = _currentCacheKey;
        var hadActiveInkOperation = IsInkOperationActive();

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

        FinalizeActiveInkOperation();
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

        if (persistToSidecar)
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

    private BitmapSource? TrySnapshotCurrentInkSurfaceForNeighbor()
    {
        if (!_inkShowEnabled || !_hasDrawing)
        {
            return null;
        }
        EnsureRasterSurface();
        if (_rasterSurface == null || _surfacePixelWidth <= 0 || _surfacePixelHeight <= 0)
        {
            return null;
        }

        var stride = _surfacePixelWidth * 4;
        var bytes = stride * _surfacePixelHeight;
        var pixels = PixelPool.Rent(bytes);
        try
        {
            _rasterSurface.CopyPixels(pixels, stride, 0);
            var snapshot = new WriteableBitmap(
                _surfacePixelWidth,
                _surfacePixelHeight,
                _surfaceDpiX,
                _surfaceDpiY,
                PixelFormats.Pbgra32,
                null);
            snapshot.WritePixels(new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight), pixels, stride, 0);
            if (snapshot.CanFreeze)
            {
                snapshot.Freeze();
            }
            return snapshot;
        }
        catch
        {
            return null;
        }
        finally
        {
            PixelPool.Return(pixels, clearArray: false);
        }
    }

    private void TrySeedNeighborFrameForInteractiveSwitch(
        int previousPage,
        BitmapSource? previousPageBitmap,
        BitmapSource? previousPageInkBitmap)
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled || previousPage <= 0)
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

        var neighborBitmap = previousPageBitmap ?? GetNeighborPageBitmapForRender(previousPage);
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

        var transform = new TransformGroup();
        transform.Children.Add(new ScaleTransform(_photoScale.ScaleX * pageScaleRatio, _photoScale.ScaleY * pageScaleRatio));
        transform.Children.Add(new TranslateTransform(_photoTranslate.X, _photoTranslate.Y + baseTop));

        var pageImg = _neighborPageImages[0];
        pageImg.Source = neighborBitmap;
        pageImg.Visibility = Visibility.Visible;
        pageImg.Uid = pageUid;
        pageImg.Tag = baseTop;
        pageImg.RenderTransform = transform;

        BitmapSource? inkBitmap = previousPageInkBitmap;
        var cacheKey = BuildNeighborInkCacheKey(previousPage);
        if (previousPageInkBitmap != null
            && !string.IsNullOrWhiteSpace(cacheKey))
        {
            RememberInteractiveSwitchInkBitmap(cacheKey, previousPage, previousPageInkBitmap);
        }
        if (inkBitmap == null
            && !string.IsNullOrWhiteSpace(cacheKey)
            && _neighborInkCache.TryGetValue(cacheKey, out var cachedEntry))
        {
            inkBitmap = cachedEntry.Bitmap;
        }
        if (inkBitmap == null
            && !string.IsNullOrWhiteSpace(cacheKey)
            && _inkShowEnabled
            && _inkCacheEnabled
            && _photoCache.TryGet(cacheKey, out var strokes)
            && strokes.Count > 0)
        {
            ScheduleNeighborInkRender(cacheKey, previousPage, neighborBitmap, strokes);
        }

        var inkImgFirst = _neighborInkImages[0];
        inkImgFirst.Source = _inkShowEnabled ? inkBitmap : null;
        inkImgFirst.Visibility = inkImgFirst.Source != null ? Visibility.Visible : Visibility.Collapsed;
        inkImgFirst.Uid = pageUid;
        inkImgFirst.Tag = baseTop;
        inkImgFirst.RenderTransform = transform;

        for (int i = 1; i < _neighborPageImages.Count; i++)
        {
            _neighborPageImages[i].Visibility = Visibility.Collapsed;
            if (i < _neighborInkImages.Count)
            {
                _neighborInkImages[i].Visibility = Visibility.Collapsed;
            }
        }

        _neighborPagesCanvas.Visibility = Visibility.Visible;
        _lastNeighborPagesNonEmptyUtc = DateTime.UtcNow;
        _inkDiagnostics?.OnCrossPageUpdateEvent(
            "seed",
            "navigate-interactive",
            $"page={previousPage} ink={(inkImgFirst.Source != null)}");
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
