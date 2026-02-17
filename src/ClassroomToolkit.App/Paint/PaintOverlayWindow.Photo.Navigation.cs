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

    private void NavigateToPage(int newPageIndex, double newTranslateY)
    {
        if (_photoDocumentIsPdf)
        {
            _currentPageIndex = newPageIndex;
            _currentCacheKey = BuildPdfCacheKey(_currentDocumentPath, _currentPageIndex);
            ResetInkHistory();
            LoadCurrentPageIfExists();
            if (!RenderPdfPage(_currentPageIndex))
            {
                return;
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
                LoadCurrentPageIfExists();
                var newBitmap = GetPageBitmap(newPageIndex);
                if (newBitmap != null)
                {
                    PhotoBackground.Source = newBitmap;
                    PhotoBackground.Visibility = Visibility.Visible;
                    UpdateCurrentPageWidthNormalization(newBitmap);
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
            ApplyCrossPageBoundaryLimits();
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
        RedrawInkSurface();
        UpdateCrossPageDisplay();
    }

    private void SaveCurrentPageIfNeeded()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
    }

    private void SaveCurrentPageOnNavigate(bool forceBackground)
    {
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        if (!forceBackground && !_inkCacheDirty)
        {
            return;
        }
        FinalizeActiveInkOperation();
        var cacheKey = _currentCacheKey;
        if (!_inkCacheEnabled || string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        var strokes = CloneCommittedInkStrokes();
        if (strokes.Count == 0)
        {
            _photoCache.Remove(cacheKey);
            _inkCacheDirty = false;
            return;
        }
        _photoCache.Set(cacheKey, strokes);
        _inkCacheDirty = false;
        System.Diagnostics.Debug.WriteLine($"[InkCache] Saved {strokes.Count} strokes for key={cacheKey}");
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
