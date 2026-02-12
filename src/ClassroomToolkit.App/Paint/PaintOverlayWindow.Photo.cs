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
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// 照片模式功能（加载、缩放、平移、跨页显示）
/// </summary>
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
        var restoreSession = false;
        if (_photoSessionPageIndex > 0
            && string.Equals(_photoSessionPath, sourcePath, StringComparison.OrdinalIgnoreCase)
            && _photoSessionIsPdf == isPdf)
        {
            restoreSession = true;
            _currentPageIndex = Math.Max(1, _photoSessionPageIndex);
            if (_rememberPhotoTransform && _photoSessionHasTransform)
            {
                _lastPhotoScaleX = _photoSessionScaleX;
                _lastPhotoScaleY = _photoSessionScaleY;
                _lastPhotoTranslateX = _photoSessionTranslateX;
                _lastPhotoTranslateY = _photoSessionTranslateY;
                _photoUserTransformDirty = true;
            }
            else
            {
                _photoUserTransformDirty = false;
            }
        }
        else
        {
            _currentPageIndex = 1;
            _photoUserTransformDirty = false;
        }
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
        if (!restoreSession)
        {
            _currentPageIndex = 1;
        }
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
        else
        {
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
            WindowState = WindowState.Maximized;
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
                    ClassroomToolkit.Interop.NativeMethods.HwndTopmost,
                    (int)Math.Round(pxRect.Left, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Top, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Width, MidpointRounding.AwayFromZero),
                    (int)Math.Round(pxRect.Height, MidpointRounding.AwayFromZero),
                    SwpNoActivate | SwpShowWindow | ClassroomToolkit.Interop.NativeMethods.SwpFrameChanged);
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
            try
            {
                await System.Threading.Tasks.Task.Delay(50).ConfigureAwait(false);
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
        });
    }

    private void ZoomPhoto(int delta, WpfPoint center)
    {
        double scaleFactor = Math.Pow(PhotoWheelZoomBase, delta);
        ApplyPhotoScale(scaleFactor, center);
        RequestInkRedraw();
    }

    private void ZoomPhotoByFactor(double scaleFactor)
    {
        var center = new WpfPoint(OverlayRoot.ActualWidth / 2.0, OverlayRoot.ActualHeight / 2.0);
        ApplyPhotoScale(scaleFactor, center);
        RequestInkRedraw();
    }

    private void ApplyPhotoScale(double scaleFactor, WpfPoint center)
    {
        EnsurePhotoTransformsWritable();
        double newScale = Math.Clamp(_photoScale.ScaleX * scaleFactor, 0.2, 4.0);
        if (Math.Abs(newScale - _photoScale.ScaleX) < 0.001)
        {
            return;
        }
        var before = ToPhotoSpace(center);
        _photoScale.ScaleX = newScale;
        _photoScale.ScaleY = newScale;
        _photoTranslate.X = center.X - before.X * newScale;
        _photoTranslate.Y = center.Y - before.Y * newScale;
        if (_crossPageDisplayEnabled)
        {
            ApplyCrossPageBoundaryLimits();
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate();
        }
        else
        {
        }
    }

    private WpfPoint ToPhotoSpace(WpfPoint point)
    {
        if (!_photoModeActive)
        {
            return point;
        }
        var inverse = GetPhotoInverseMatrix();
        return inverse.Transform(point);
    }

    private Geometry? ToPhotoGeometry(Geometry geometry)
    {
        if (!_photoModeActive || geometry == null)
        {
            return geometry;
        }
        var inverse = GetPhotoInverseMatrix();
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(inverse);
        var flattened = clone.GetFlattenedPathGeometry();
        if (flattened.CanFreeze)
        {
            flattened.Freeze();
        }
        return flattened;
    }

    private Geometry? ToScreenGeometry(Geometry geometry)
    {
        if (!_photoModeActive || geometry == null)
        {
            return geometry;
        }
        var transform = GetPhotoMatrix();
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(transform);
        if (clone.CanFreeze)
        {
            clone.Freeze();
        }
        return clone;
    }

    private Matrix GetPhotoMatrix()
    {
        var matrix = Matrix.Identity;
        matrix.Scale(_photoPageScale.ScaleX * _photoScale.ScaleX, _photoPageScale.ScaleY * _photoScale.ScaleY);
        matrix.Translate(_photoTranslate.X, _photoTranslate.Y);
        return matrix;
    }

    private Matrix GetPhotoInverseMatrix()
    {
        var scaleX = _photoPageScale.ScaleX * _photoScale.ScaleX;
        var scaleY = _photoPageScale.ScaleY * _photoScale.ScaleY;
        if (Math.Abs(scaleX) < 0.0001 || Math.Abs(scaleY) < 0.0001)
        {
            return Matrix.Identity;
        }
        var matrix = Matrix.Identity;
        matrix.Scale(1.0 / scaleX, 1.0 / scaleY);
        matrix.Translate(-_photoTranslate.X / scaleX, -_photoTranslate.Y / scaleY);
        return matrix;
    }

    private bool TryBeginPhotoPan(MouseButtonEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor || IsBoardActive())
        {
            return false;
        }
        _photoPanning = true;
        _photoPanStart = e.GetPosition(OverlayRoot);
        _photoPanOriginX = _photoTranslate.X;
        _photoPanOriginY = _photoTranslate.Y;
        OverlayRoot.CaptureMouse();
        e.Handled = true;
        return true;
    }

    private void UpdatePhotoPan(WpfPoint point)
    {
        if (!_photoPanning)
        {
            return;
        }
        EnsurePhotoTransformsWritable();
        var delta = point - _photoPanStart;
        _photoTranslate.X = _photoPanOriginX + delta.X;
        _photoTranslate.Y = _photoPanOriginY + delta.Y;
        // Enable cross-page display when dragging vertically
        if (_crossPageDisplayEnabled)
        {
            if (Math.Abs(delta.Y) > 5)
            {
                _crossPageDragging = true;
            }
            ApplyCrossPageBoundaryLimits();
        }
        UpdateNeighborTransformsForPan();
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate();
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        RequestInkRedraw();
        if (!_crossPageDisplayEnabled)
        {
        }
    }

    private void ApplyCrossPageBoundaryLimits()
    {
        if (!_crossPageDisplayEnabled || !_photoModeActive)
        {
            return;
        }
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null
            || !TryGetCrossPageBounds(currentBitmap, out _, out _, out var minY, out var maxY, out _))
        {
            return;
        }

        var originalY = _photoTranslate.Y;
        _photoTranslate.Y = Math.Clamp(_photoTranslate.Y, minY, maxY);
        _crossPageTranslateClamped = Math.Abs(originalY - _photoTranslate.Y) > 0.5;
    }

    private bool TryGetCrossPageBounds(
        BitmapSource currentBitmap,
        out double minX,
        out double maxX,
        out double minY,
        out double maxY,
        out double normalizedWidthDip)
    {
        minX = maxX = minY = maxY = 0;
        normalizedWidthDip = 0;

        if (!_crossPageDisplayEnabled || !_photoModeActive)
        {
            return false;
        }
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return false;
        }

        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var viewportWidth = OverlayRoot.ActualWidth;
        if (viewportWidth <= 0)
        {
            viewportWidth = ActualWidth;
        }
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return false;
        }

        var currentPage = GetCurrentPageIndexForCrossPage();
        normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var currentPageHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        if (currentPageHeight <= 0)
        {
            return false;
        }

        double totalHeightAbove = 0;
        for (int i = 1; i < currentPage; i++)
        {
            var height = GetScaledHeightForPage(i, normalizedWidthDip);
            if (height > 0)
            {
                totalHeightAbove += height;
            }
        }
        double totalHeightBelow = 0;
        for (int i = currentPage + 1; i <= totalPages; i++)
        {
            var height = GetScaledHeightForPage(i, normalizedWidthDip);
            if (height > 0)
            {
                totalHeightBelow += height;
            }
        }

        maxY = totalHeightAbove;
        minY = -(currentPageHeight + totalHeightBelow - viewportHeight);

        var slack = Math.Max(32.0, viewportHeight * 0.5);
        if (currentPage > 1)
        {
            maxY += slack;
        }
        if (currentPage < totalPages)
        {
            minY -= slack;
        }
        if (minY > maxY)
        {
            var middle = (minY + maxY) * 0.5;
            minY = middle;
            maxY = middle;
        }

        if (normalizedWidthDip > 0)
        {
            var scaledWidth = normalizedWidthDip * _photoScale.ScaleX;
            var horizontalSlack = Math.Max(48.0, viewportWidth * 0.08);
            if (scaledWidth <= viewportWidth)
            {
                var centerX = (viewportWidth - scaledWidth) * 0.5;
                minX = centerX - horizontalSlack;
                maxX = centerX + horizontalSlack;
            }
            else
            {
                minX = (viewportWidth - scaledWidth) - horizontalSlack;
                maxX = horizontalSlack;
            }
        }
        else
        {
            minX = -1_000_000;
            maxX = 1_000_000;
        }

        return true;
    }

    private void EndPhotoPan()
    {
        if (!_photoPanning)
        {
            return;
        }
        _photoPanning = false;
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
        if (_crossPageDragging && _crossPageDisplayEnabled)
        {
            _crossPageDragging = false;
            _crossPageTranslateClamped = false;
            FinalizeCurrentPageFromScroll();
        }
        FlushPhotoTransformSave();
        RequestInkRedraw();
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

    private void ExecutePhotoClose()
    {
        PhotoCloseRequested?.Invoke();
        if (_photoModeActive)
        {
            // Keep overlay available for desktop/PPT inking: closing photo only exits photo mode.
            ExitPhotoMode();
            return;
        }
        WindowState = WindowState.Minimized;
    }

    private void EnsurePhotoTransformsWritable()
    {
        if (_photoPageScale.IsFrozen)
        {
            _photoPageScale = _photoPageScale.Clone();
        }
        if (_photoScale.IsFrozen)
        {
            _photoScale = _photoScale.Clone();
        }
        if (_photoTranslate.IsFrozen)
        {
            _photoTranslate = _photoTranslate.Clone();
        }
    }

    // Cross-page display helper methods
    private void ClearNeighborPages()
    {
        if (_neighborPagesCanvas == null)
        {
            return;
        }
        _neighborPagesCanvas.Children.Clear();
        _neighborPagesCanvas.Visibility = Visibility.Collapsed;
        _neighborPageImages.Clear();
        _neighborInkImages.Clear();
    }

    private void ClearNeighborImageCache()
    {
        _neighborImageCache.Clear();
        _neighborInkCache.Clear();
    }

    private int GetTotalPageCount()
    {
        if (_photoDocumentIsPdf)
        {
            return _pdfPageCount;
        }
        return _photoSequencePaths.Count;
    }

    private int GetCurrentPageIndexForCrossPage()
    {
        if (_photoDocumentIsPdf)
        {
            return _currentPageIndex;
        }
        return _photoSequenceIndex >= 0 ? _photoSequenceIndex + 1 : 1;
    }

    private void SetCurrentPageIndexForCrossPage(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            _currentPageIndex = pageIndex;
        }
        else
        {
            _photoSequenceIndex = pageIndex - 1;
        }
    }

    private BitmapSource? GetPageBitmap(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return GetPdfPageBitmap(pageIndex);
        }
        // For image sequence, pageIndex is 1-based
        var arrayIndex = pageIndex - 1;
        if (arrayIndex < 0 || arrayIndex >= _photoSequencePaths.Count)
        {
            return null;
        }
        if (_neighborImageCache.TryGetValue(pageIndex, out var cached))
        {
            return cached;
        }
        var path = _photoSequencePaths[arrayIndex];
        var bitmap = TryLoadBitmapSource(path, downsampleToMonitor: _crossPageDisplayEnabled);
        if (bitmap != null)
        {
            _neighborImageCache[pageIndex] = bitmap;
            // Limit cache size
            if (_neighborImageCache.Count > NeighborPageCacheLimit + 2)
            {
                var keysToRemove = _neighborImageCache.Keys
                    .OrderBy(k => Math.Abs(k - pageIndex))
                    .Skip(NeighborPageCacheLimit)
                    .ToList();
                foreach (var k in keysToRemove)
                {
                    _neighborImageCache.Remove(k);
                }
            }
        }
        return bitmap;
    }

    private BitmapSource? GetNeighborPageBitmap(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return TryGetCachedPdfPageBitmap(pageIndex, out var cached) ? cached : null;
        }
        return GetPageBitmap(pageIndex);
    }

    private BitmapSource? GetNeighborPageBitmapForRender(int pageIndex)
    {
        if (!_photoDocumentIsPdf)
        {
            return GetPageBitmap(pageIndex);
        }

        // In cross-page mode, a cache miss can leave a large blank gap between pages.
        // Fallback to direct render once so visible neighbors are always drawable.
        if (TryGetCachedPdfPageBitmap(pageIndex, out var cached))
        {
            return cached;
        }
        return GetPdfPageBitmap(pageIndex);
    }

    private BitmapSource? TryLoadBitmapSource(string path, bool downsampleToMonitor = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            
            // Limit decoding resolution to prevent OOM in cross-page mode.
            // Important: decode width must only downsample large sources, never upscale.
            if (downsampleToMonitor)
            {
                // Use 1.5x of monitor width as a safe buffer for zooming.
                var monitorRect = GetCurrentMonitorRect();
                if (monitorRect.Width > 0)
                {
                    var targetDecodeWidth = (int)Math.Round(monitorRect.Width * 1.5, MidpointRounding.AwayFromZero);
                    var sourcePixelWidth = TryReadImagePixelWidth(path);
                    if (targetDecodeWidth > 0
                        && sourcePixelWidth > 0
                        && sourcePixelWidth > targetDecodeWidth)
                    {
                        bitmap.DecodePixelWidth = targetDecodeWidth;
                    }
                }
            }

            bitmap.EndInit();
            bitmap.Freeze();
            if (RequiresPixelFormatNormalization(bitmap.Format))
            {
                var converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = bitmap;
                converted.DestinationFormat = PixelFormats.Bgr32;
                converted.EndInit();
                converted.Freeze();
                return converted;
            }
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static bool RequiresPixelFormatNormalization(PixelFormat format)
    {
        return format == PixelFormats.BlackWhite
               || format == PixelFormats.Indexed1
               || format == PixelFormats.Indexed2
               || format == PixelFormats.Indexed4
               || format == PixelFormats.Indexed8
               || format == PixelFormats.Gray2
               || format == PixelFormats.Gray4
               || format == PixelFormats.Gray8;
    }

    private static int TryReadImagePixelWidth(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
            {
                return 0;
            }
            return decoder.Frames[0].PixelWidth;
        }
        catch
        {
            return 0;
        }
    }

    private void ScheduleNeighborImagePrefetch(int pageIndex)
    {
        if (!_photoModeActive || _photoDocumentIsPdf)
        {
            return;
        }
        if (!_crossPageDisplayEnabled && (_photoPanning || _crossPageDragging))
        {
            return;
        }
        if (_photoSequencePaths.Count == 0 || pageIndex < 1 || pageIndex > _photoSequencePaths.Count)
        {
            return;
        }
        if (_neighborImageCache.ContainsKey(pageIndex))
        {
            return;
        }
        if (!_neighborImagePrefetchPending.Add(pageIndex))
        {
            return;
        }
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                if (!_photoModeActive || _photoDocumentIsPdf || _crossPageDragging)
                {
                    return;
                }
                if (_neighborImageCache.ContainsKey(pageIndex))
                {
                    return;
                }
                var path = _photoSequencePaths[pageIndex - 1];
                var bitmap = TryLoadBitmapSource(path, downsampleToMonitor: true);
                if (bitmap == null)
                {
                    return;
                }
                _neighborImageCache[pageIndex] = bitmap;
                if (_neighborImageCache.Count > NeighborPageCacheLimit + 2)
                {
                    var keysToRemove = _neighborImageCache.Keys
                        .OrderBy(k => Math.Abs(k - pageIndex))
                        .Skip(NeighborPageCacheLimit)
                        .ToList();
                    foreach (var k in keysToRemove)
                    {
                        _neighborImageCache.Remove(k);
                    }
                }
            }
            finally
            {
                _neighborImagePrefetchPending.Remove(pageIndex);
            }
        }, DispatcherPriority.Background);
    }

    private double GetScaledPageHeight(BitmapSource? bitmap, double normalizedWidthDip = 0)
    {
        if (bitmap == null)
        {
            return 0;
        }
        var imageHeight = GetBitmapDisplayHeightInDip(bitmap);
        if (!_photoDocumentIsPdf && normalizedWidthDip > 0)
        {
            var imageWidth = GetBitmapDisplayWidthInDip(bitmap);
            if (imageWidth > 0)
            {
                imageHeight *= normalizedWidthDip / imageWidth;
            }
        }
        return imageHeight * _photoScale.ScaleY;
    }

    private double GetBitmapDisplayWidthInDip(BitmapSource bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width > 0 && height > 0)
        {
            if (bitmap is BitmapImage bi && (bi.Rotation == Rotation.Rotate90 || bi.Rotation == Rotation.Rotate270))
            {
                return height;
            }
            return width;
        }
        var dpiX = bitmap.DpiX > 0 ? bitmap.DpiX : 96.0;
        var fallbackWidth = bitmap.PixelWidth * 96.0 / dpiX;
        if (bitmap is BitmapImage rotated && (rotated.Rotation == Rotation.Rotate90 || rotated.Rotation == Rotation.Rotate270))
        {
            var dpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 96.0;
            return bitmap.PixelHeight * 96.0 / dpiY;
        }
        return fallbackWidth;
    }

    private double GetBitmapDisplayHeightInDip(BitmapSource bitmap)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        if (width > 0 && height > 0)
        {
            if (bitmap is BitmapImage bi && (bi.Rotation == Rotation.Rotate90 || bi.Rotation == Rotation.Rotate270))
            {
                return width;
            }
            return height;
        }
        var dpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 96.0;
        var fallbackHeight = bitmap.PixelHeight * 96.0 / dpiY;
        if (bitmap is BitmapImage rotated && (rotated.Rotation == Rotation.Rotate90 || rotated.Rotation == Rotation.Rotate270))
        {
            var dpiX = bitmap.DpiX > 0 ? bitmap.DpiX : 96.0;
            return bitmap.PixelWidth * 96.0 / dpiX;
        }
        return fallbackHeight;
    }

    private void ResetCrossPageNormalizedWidth()
    {
        _crossPageNormalizedWidthDip = 0;
    }

    private double GetCrossPageNormalizedWidthDip(BitmapSource? fallback = null)
    {
        if (!_crossPageDisplayEnabled || _photoDocumentIsPdf)
        {
            return 0;
        }
        if (_crossPageNormalizedWidthDip > 1)
        {
            return _crossPageNormalizedWidthDip;
        }
        var bitmap = fallback ?? PhotoBackground.Source as BitmapSource;
        if (bitmap == null)
        {
            return 0;
        }
        var widthDip = GetBitmapDisplayWidthInDip(bitmap);
        if (widthDip <= 1)
        {
            return 0;
        }
        _crossPageNormalizedWidthDip = widthDip;
        return _crossPageNormalizedWidthDip;
    }

    private void UpdateCurrentPageWidthNormalization(BitmapSource? bitmap = null)
    {
        EnsurePhotoTransformsWritable();
        if (!_crossPageDisplayEnabled || _photoDocumentIsPdf)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            return;
        }
        var source = bitmap ?? PhotoBackground.Source as BitmapSource;
        if (source == null)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            return;
        }
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(source);
        var pageWidthDip = GetBitmapDisplayWidthInDip(source);
        if (normalizedWidthDip <= 0 || pageWidthDip <= 0)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            return;
        }
        var ratio = normalizedWidthDip / pageWidthDip;
        _photoPageScale.ScaleX = ratio;
        _photoPageScale.ScaleY = ratio;
    }

    private void UpdateCrossPageDisplay()
    {
        if (!_crossPageDisplayEnabled || !_photoModeActive)
        {
            return;
        }
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            return;
        }
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var currentPageHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        if (currentPageHeight <= 0)
        {
            return;
        }
        var currentTop = _photoTranslate.Y;
        var currentBottom = currentTop + currentPageHeight;

        // Dynamically collect all pages intersecting viewport to avoid missing strips
        // when zoomed out or when page heights vary significantly.
        const double visibilityMargin = 2.0;
        var visiblePages = new List<(int PageIndex, double Top)>
        {
            (currentPage, currentTop)
        };

        var prevTop = currentTop;
        for (int pageIndex = currentPage - 1; pageIndex >= 1; pageIndex--)
        {
            var prevHeight = GetScaledHeightForPage(pageIndex, normalizedWidthDip);
            if (prevHeight <= 0)
            {
                break;
            }
            prevTop -= prevHeight;
            var prevBottom = prevTop + prevHeight;
            if (prevBottom < -visibilityMargin)
            {
                break;
            }
            visiblePages.Insert(0, (pageIndex, prevTop));
        }

        var nextTop = currentBottom;
        for (int pageIndex = currentPage + 1; pageIndex <= totalPages; pageIndex++)
        {
            if (nextTop > viewportHeight + visibilityMargin)
            {
                break;
            }
            visiblePages.Add((pageIndex, nextTop));
            var nextHeight = GetScaledHeightForPage(pageIndex, normalizedWidthDip);
            if (nextHeight <= 0)
            {
                break;
            }
            nextTop += nextHeight;
        }
        if (_photoDocumentIsPdf)
        {
            var missingPages = visiblePages
                .Where(p => p.PageIndex != currentPage)
                .Select(p => p.PageIndex)
                .Distinct()
                .Where(p => !TryGetCachedPdfPageBitmap(p, out _))
                .ToList();
            if (missingPages.Count > 0)
            {
                SchedulePdfVisiblePrefetch(missingPages);
            }
            _pdfPinnedPages.Clear();
            foreach (var page in visiblePages.Select(p => p.PageIndex).Distinct())
            {
                _pdfPinnedPages.Add(page);
            }
        }
        if (!_photoDocumentIsPdf)
        {
            ScheduleNeighborImagePrefetch(currentPage - 1);
            ScheduleNeighborImagePrefetch(currentPage + 1);
        }
        // Render neighbor pages
        var neighborPages = visiblePages.Where(p => p.PageIndex != currentPage).ToList();
        if (_crossPageDragging && _crossPageTranslateClamped && neighborPages.Count == 0)
        {
            return;
        }
        RenderNeighborPages(neighborPages);
    }

    private double GetScaledHeightForPage(int pageIndex, double normalizedWidthDip = 0)
    {
        if (pageIndex <= 0)
        {
            return 0;
        }
        if (_photoDocumentIsPdf)
        {
            return GetScaledPdfPageHeight(pageIndex);
        }
        return GetScaledPageHeight(GetPageBitmap(pageIndex), normalizedWidthDip);
    }

    private void RenderNeighborPages(List<(int PageIndex, double Top)> neighborPages)
    {
        if (neighborPages.Count == 0)
        {
            ClearNeighborPages();
            return;
        }
        if (_neighborPagesCanvas == null)
        {
            return;
        }
        _neighborPagesCanvas.Visibility = Visibility.Visible;
        var normalizedWidthDip = 0.0;
        if (!_photoDocumentIsPdf && PhotoBackground.Source is BitmapSource currentBitmap)
        {
            normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        }
        // Ensure we have enough Image elements
        while (_neighborPageImages.Count < neighborPages.Count)
        {
            var img = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top
            };
            _neighborPageImages.Add(img);
            _neighborPagesCanvas.Children.Add(img);
        }
        while (_neighborInkImages.Count < neighborPages.Count)
        {
            var inkImg = new WpfImage
            {
                Stretch = Stretch.None,
                SnapsToDevicePixels = true,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                VerticalAlignment = System.Windows.VerticalAlignment.Top,
                IsHitTestVisible = false
            };
            _neighborInkImages.Add(inkImg);
            _neighborPagesCanvas.Children.Add(inkImg);
        }
        // Hide excess images
        for (int i = neighborPages.Count; i < _neighborPageImages.Count; i++)
        {
            _neighborPageImages[i].Visibility = Visibility.Collapsed;
            if (i < _neighborInkImages.Count)
            {
                _neighborInkImages[i].Visibility = Visibility.Collapsed;
            }
        }
        // Update visible neighbor page images
        for (int i = 0; i < neighborPages.Count; i++)
        {
            var (pageIndex, top) = neighborPages[i];
            var bitmap = GetNeighborPageBitmapForRender(pageIndex);
            var img = _neighborPageImages[i];
            img.Source = bitmap;
            img.Visibility = bitmap != null ? Visibility.Visible : Visibility.Collapsed;
            var inkImg = _neighborInkImages[i];
            if (bitmap != null && !_photoPanning && !_crossPageDragging)
            {
                var inkBitmap = TryGetNeighborInkBitmap(pageIndex, bitmap);
                inkImg.Source = inkBitmap;
                inkImg.Visibility = inkBitmap != null ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                inkImg.Source = null;
                inkImg.Visibility = Visibility.Collapsed;
            }
            if (bitmap != null)
            {
                var baseTop = top - _photoTranslate.Y;
                var pageScaleRatio = 1.0;
                if (!_photoDocumentIsPdf && normalizedWidthDip > 0)
                {
                    var pageWidthDip = GetBitmapDisplayWidthInDip(bitmap);
                    if (pageWidthDip > 0)
                    {
                        pageScaleRatio = normalizedWidthDip / pageWidthDip;
                    }
                }
                img.Tag = baseTop;
                inkImg.Tag = baseTop;
                // Apply same transform as current page
                var transform = new TransformGroup();
                transform.Children.Add(new ScaleTransform(_photoScale.ScaleX * pageScaleRatio, _photoScale.ScaleY * pageScaleRatio));
                transform.Children.Add(new TranslateTransform(_photoTranslate.X, _photoTranslate.Y + baseTop));
                img.RenderTransform = transform;
                inkImg.RenderTransform = transform;
            }
        }
    }

    private void UpdateNeighborTransformsForPan()
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled)
        {
            return;
        }
        if (_neighborPageImages.Count == 0 || _neighborInkImages.Count == 0)
        {
            return;
        }
        for (int i = 0; i < _neighborPageImages.Count; i++)
        {
            var img = _neighborPageImages[i];
            if (img.Visibility != Visibility.Visible || img.RenderTransform is not TransformGroup group)
            {
                continue;
            }
            if (img.Tag is not double baseTop || group.Children.Count < 2)
            {
                continue;
            }
            if (group.Children[1] is TranslateTransform translate)
            {
                translate.X = _photoTranslate.X;
                translate.Y = _photoTranslate.Y + baseTop;
            }
            if (i < _neighborInkImages.Count)
            {
                var inkImg = _neighborInkImages[i];
                if (inkImg.Visibility != Visibility.Visible || inkImg.RenderTransform is not TransformGroup inkGroup)
                {
                    continue;
                }
                if (inkGroup.Children.Count < 2)
                {
                    continue;
                }
                if (inkGroup.Children[1] is TranslateTransform inkTranslate)
                {
                    inkTranslate.X = _photoTranslate.X;
                    inkTranslate.Y = _photoTranslate.Y + baseTop;
                }
            }
        }
    }

    private void FinalizeCurrentPageFromScroll()
    {
        if (!_crossPageDisplayEnabled)
        {
            return;
        }
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return;
        }
        var currentPage = GetCurrentPageIndexForCrossPage();
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        if (currentBitmap == null)
        {
            return;
        }
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        var viewportCenter = viewportHeight / 2;
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var currentPageHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        var currentTop = _photoTranslate.Y;
        var currentBottom = currentTop + currentPageHeight;

        // Determine which page contains the viewport center
        int newCurrentPage = currentPage;
        double newTranslateY = currentTop;

        if (currentTop > viewportCenter && currentPage > 1)
        {
            // Previous page is at center
            newCurrentPage = currentPage - 1;
            var newHeight = GetScaledHeightForPage(newCurrentPage, normalizedWidthDip);
            newTranslateY = currentTop - newHeight;
        }
        else if (currentBottom < viewportCenter && currentPage < totalPages)
        {
            // Next page is at center
            newCurrentPage = currentPage + 1;
            var newHeight = GetScaledHeightForPage(newCurrentPage, normalizedWidthDip);
            newTranslateY = currentTop + currentPageHeight;
        }

        if (newCurrentPage != currentPage)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
            NavigateToPage(newCurrentPage, newTranslateY);
        }
        else
        {
            UpdateCrossPageDisplay();
        }
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
        // 尝试 PDF 内部导航
        if (TryNavigatePdf(-1))
        {
            return;
        }
        // 触发外部导航事件 (MainWindow 会处理文件间切换)
        if (!IsAtFileSequenceBoundary(-1))
        {
            PhotoNavigationRequested?.Invoke(-1);
        }
    }

    private void OnPhotoNextClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        // 尝试 PDF 内部导航
        if (TryNavigatePdf(1))
        {
            return;
        }
        // 触发外部导航事件 (MainWindow 会处理文件间切换)
        if (!IsAtFileSequenceBoundary(1))
        {
            PhotoNavigationRequested?.Invoke(1);
        }
    }

    private BitmapSource? TryGetNeighborInkBitmap(int pageIndex, BitmapSource pageBitmap)
    {
        if (!_inkCacheEnabled || pageBitmap.PixelWidth <= 0 || pageBitmap.PixelHeight <= 0)
        {
            return null;
        }
        var cacheKey = BuildNeighborInkCacheKey(pageIndex);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return null;
        }
        if (!_photoCache.TryGet(cacheKey, out var strokes) || strokes.Count == 0)
        {
            _neighborInkCache.Remove(cacheKey);
            return null;
        }
        if (_neighborInkCache.TryGetValue(cacheKey, out var entry) && ReferenceEquals(entry.Strokes, strokes))
        {
            return entry.Bitmap;
        }
        ScheduleNeighborInkRender(cacheKey, pageIndex, pageBitmap, strokes);
        return null;
    }

    private void ScheduleNeighborInkRender(
        string cacheKey,
        int pageIndex,
        BitmapSource pageBitmap,
        List<InkStrokeData> strokes)
    {
        if (_neighborInkRenderPending.Contains(cacheKey))
        {
            return;
        }
        _neighborInkRenderPending.Add(cacheKey);
        var scheduled = TryBeginInvoke(() =>
        {
            try
            {
                if (!_photoModeActive || !_crossPageDisplayEnabled)
                {
                    return;
                }
                if (!_photoCache.TryGet(cacheKey, out var currentStrokes) || currentStrokes.Count == 0)
                {
                    _neighborInkCache.Remove(cacheKey);
                    return;
                }
                if (_neighborInkCache.TryGetValue(cacheKey, out var existing) && ReferenceEquals(existing.Strokes, currentStrokes))
                {
                    return;
                }
                var page = new InkPageData
                {
                    PageIndex = pageIndex,
                    DocumentName = _currentDocumentName,
                    SourcePath = _currentDocumentPath,
                    Strokes = currentStrokes
                };
                var bitmap = _inkStrokeRenderer.RenderPage(
                    page,
                    pageBitmap.PixelWidth,
                    pageBitmap.PixelHeight,
                    pageBitmap.DpiX,
                    pageBitmap.DpiY);
                _neighborInkCache[cacheKey] = new InkBitmapCacheEntry(currentStrokes, bitmap);
                RequestCrossPageDisplayUpdate();
            }
            finally
            {
                _neighborInkRenderPending.Remove(cacheKey);
            }
        }, DispatcherPriority.Background);
        if (!scheduled)
        {
            _neighborInkRenderPending.Remove(cacheKey);
        }
    }

    private string BuildNeighborInkCacheKey(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return BuildPdfCacheKey(_currentDocumentPath, pageIndex);
        }
        var arrayIndex = pageIndex - 1;
        if (arrayIndex < 0 || arrayIndex >= _photoSequencePaths.Count)
        {
            return string.Empty;
        }
        return BuildPhotoCacheKey(_photoSequencePaths[arrayIndex]);
    }

    private sealed record InkBitmapCacheEntry(List<InkStrokeData> Strokes, BitmapSource Bitmap);

    private bool TrySetPhotoBackground(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            PhotoBackground.Source = null;
            PhotoBackground.Visibility = Visibility.Collapsed;
            return false;
        }
        try
        {
            var bitmap = TryLoadBitmapSource(imagePath, downsampleToMonitor: _crossPageDisplayEnabled);
            if (bitmap == null)
            {
                PhotoBackground.Source = null;
                PhotoBackground.Visibility = Visibility.Collapsed;
                return false;
            }
            PhotoBackground.Source = bitmap;
            PhotoBackground.Visibility = Visibility.Visible;
            UpdateCurrentPageWidthNormalization(bitmap);
            if (_crossPageDisplayEnabled)
            {
                if (_photoUnifiedTransformReady)
                {
                    EnsurePhotoTransformsWritable();
                    _photoScale.ScaleX = _lastPhotoScaleX;
                    _photoScale.ScaleY = _lastPhotoScaleY;
                    _photoTranslate.X = _lastPhotoTranslateX;
                    _photoTranslate.Y = _lastPhotoTranslateY;
                }
                else
                {
                    ApplyPhotoFitToViewport(bitmap);
                }
                return true;
            }
            var appliedStored = TryApplyStoredPhotoTransform(GetCurrentPhotoTransformKey());
            if (!appliedStored)
            {
                ApplyPhotoFitToViewport(bitmap);
            }
            return true;
        }
        catch
        {
            PhotoBackground.Source = null;
            PhotoBackground.Visibility = Visibility.Collapsed;
            return false;
        }
    }

    private bool IsAtFileSequenceBoundary(int direction)
    {
        if (_photoSequencePaths.Count == 0)
        {
            return true;
        }
        var next = _photoSequenceIndex + direction;
        return next < 0 || next >= _photoSequencePaths.Count;
    }

    private void ApplyPhotoFitToViewport(BitmapSource bitmap, double? dpiOverride = null)
    {
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return;
        }
        EnsurePhotoTransformsWritable();
        var viewportWidth = OverlayRoot.ActualWidth;
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            viewportWidth = PhotoWindowFrame.ActualWidth;
            viewportHeight = PhotoWindowFrame.ActualHeight;
        }
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            var monitor = GetCurrentMonitorRectInDip(useWorkArea: false);
            viewportWidth = monitor.Width;
            viewportHeight = monitor.Height;
        }
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            return;
        }
        double imageWidth;
        double imageHeight;
        if (_photoDocumentIsPdf)
        {
            var dpiX = dpiOverride.HasValue && dpiOverride.Value > 0 ? dpiOverride.Value : bitmap.DpiX;
            var dpiY = dpiOverride.HasValue && dpiOverride.Value > 0 ? dpiOverride.Value : bitmap.DpiY;
            imageWidth = dpiX > 0 ? bitmap.PixelWidth * 96.0 / dpiX : bitmap.PixelWidth;
            imageHeight = dpiY > 0 ? bitmap.PixelHeight * 96.0 / dpiY : bitmap.PixelHeight;
        }
        else
        {
            imageWidth = GetBitmapDisplayWidthInDip(bitmap);
            imageHeight = GetBitmapDisplayHeightInDip(bitmap);
        }

        if (bitmap is BitmapImage bi && (bi.Rotation == Rotation.Rotate90 || bi.Rotation == Rotation.Rotate270))
        {
            (imageWidth, imageHeight) = (imageHeight, imageWidth);
        }

        var scaleX = viewportWidth / imageWidth;
        var scaleY = viewportHeight / imageHeight;
        var scale = Math.Min(scaleX, scaleY);
        _photoScale.ScaleX = scale;
        _photoScale.ScaleY = scale;
        var scaledWidth = imageWidth * scale;
        var scaledHeight = imageHeight * scale;
        _photoTranslate.X = (viewportWidth - scaledWidth) / 2.0;
        _photoTranslate.Y = (viewportHeight - scaledHeight) / 2.0;
        SavePhotoTransformState(userAdjusted: false);
        RequestInkRedraw();
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        EnsureRasterSurface();
        if (!_photoModeActive || _photoUserTransformDirty)
        {
            return;
        }
        if (PhotoBackground.Source is BitmapSource bitmap)
        {
            ApplyPhotoFitToViewport(bitmap);
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        if (WindowState == WindowState.Minimized)
        {
            _photoRestoreFullscreenPending = true;
            // Save current zoom/pan state before minimizing
            SavePhotoTransformState(true);
            return;
        }
        if (_photoRestoreFullscreenPending)
        {
            _photoRestoreFullscreenPending = false;
            _photoFullscreen = true;
            SetPhotoWindowMode(fullscreen: true);

            // Restore PDF page rendering
            if (_photoDocumentIsPdf && _pdfDocument != null)
            {
                RenderPdfPage(_currentPageIndex);
            }

            // Restore zoom/pan state if remember transform is enabled
            if (_rememberPhotoTransform)
            {
                var key = GetCurrentPhotoTransformKey();
                if (!_crossPageDisplayEnabled && TryApplyStoredPhotoTransform(key))
                {
                }
                else
                {
                    EnsurePhotoTransformsWritable();
                    _photoScale.ScaleX = _lastPhotoScaleX;
                    _photoScale.ScaleY = _lastPhotoScaleY;
                    _photoTranslate.X = _lastPhotoTranslateX;
                    _photoTranslate.Y = _lastPhotoTranslateY;
                }
            }
        }
    }

    private readonly struct PhotoTransformState
    {
        public PhotoTransformState(double scaleX, double scaleY, double translateX, double translateY, bool userAdjusted)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            TranslateX = translateX;
            TranslateY = translateY;
            UserAdjusted = userAdjusted;
        }

        public double ScaleX { get; }
        public double ScaleY { get; }
        public double TranslateX { get; }
        public double TranslateY { get; }
        public bool UserAdjusted { get; }
    }

    private string GetCurrentPhotoTransformKey()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return string.Empty;
        }
        return BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, _photoDocumentIsPdf);
    }

    private bool TryApplyStoredPhotoTransform(string cacheKey)
    {
        if (_crossPageDisplayEnabled)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _photoUserTransformDirty = false;
            return false;
        }
        if (!_photoPageTransforms.TryGetValue(cacheKey, out var state))
        {
            _photoUserTransformDirty = false;
            return false;
        }
        EnsurePhotoTransformsWritable();
        _photoScale.ScaleX = state.ScaleX;
        _photoScale.ScaleY = state.ScaleY;
        _photoTranslate.X = state.TranslateX;
        _photoTranslate.Y = state.TranslateY;
        _photoUserTransformDirty = state.UserAdjusted;
        return true;
    }

    private void SavePhotoTransformState(bool userAdjusted)
    {
        _lastPhotoScaleX = _photoScale.ScaleX;
        _lastPhotoScaleY = _photoScale.ScaleY;
        _lastPhotoTranslateX = _photoTranslate.X;
        _lastPhotoTranslateY = _photoTranslate.Y;
        _photoUserTransformDirty = userAdjusted;
        if (_crossPageDisplayEnabled)
        {
            _photoUnifiedTransformReady = true;
            SchedulePhotoUnifiedTransformSave();
            return;
        }
        if (_rememberPhotoTransform && _photoModeActive)
        {
            var key = GetCurrentPhotoTransformKey();
            if (!string.IsNullOrWhiteSpace(key))
            {
                _photoPageTransforms[key] = new PhotoTransformState(
                    _photoScale.ScaleX,
                    _photoScale.ScaleY,
                    _photoTranslate.X,
                    _photoTranslate.Y,
                    userAdjusted);
            }
        }
    }

    private void SavePhotoSession()
    {
        if (!_photoModeActive || string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }
        _photoSessionPath = _currentDocumentPath;
        _photoSessionIsPdf = _photoDocumentIsPdf;
        _photoSessionPageIndex = _currentPageIndex;
        if (_rememberPhotoTransform && _photoUserTransformDirty)
        {
            _photoSessionHasTransform = true;
            _photoSessionScaleX = _photoScale.ScaleX;
            _photoSessionScaleY = _photoScale.ScaleY;
            _photoSessionTranslateX = _photoTranslate.X;
            _photoSessionTranslateY = _photoTranslate.Y;
        }
        else
        {
            _photoSessionHasTransform = false;
        }
    }

    private static string BuildPhotoCacheKey(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }
        try
        {
            return $"img|{IoPath.GetFullPath(sourcePath)}";
        }
        catch
        {
            return $"img|{sourcePath}";
        }
    }

    private string BuildPhotoModeCacheKey(string sourcePath, int pageIndex, bool isPdf)
    {
        if (!isPdf)
        {
            return BuildPhotoCacheKey(sourcePath);
        }
        return BuildPdfCacheKey(sourcePath, pageIndex);
    }

    private static string BuildPdfCacheKey(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return string.Empty;
        }
        try
        {
            return $"pdf|{IoPath.GetFullPath(sourcePath)}|page_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
        }
        catch
        {
            return $"pdf|{sourcePath}|page_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
        }
    }

    private static bool IsPdfFile(string path)
    {
        var ext = IoPath.GetExtension(path);
        return !string.IsNullOrWhiteSpace(ext) && ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private void RequestCrossPageDisplayUpdate()
    {
        if (_crossPageUpdatePending)
        {
            return;
        }
        var nowUtc = DateTime.UtcNow;
        var throttleActive = _photoPanning || _crossPageDragging;
        var elapsedMs = (nowUtc - _lastCrossPageUpdateUtc).TotalMilliseconds;
        if (throttleActive && elapsedMs < CrossPageUpdateMinIntervalMs)
        {
            _crossPageUpdatePending = true;
            var token = Interlocked.Increment(ref _crossPageUpdateToken);
            var delay = Math.Max(1, (int)Math.Ceiling(CrossPageUpdateMinIntervalMs - elapsedMs));
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
                var scheduled = TryBeginInvoke(() =>
                {
                    if (token != _crossPageUpdateToken)
                    {
                        return;
                    }
                    _crossPageUpdatePending = false;
                    if (!_photoModeActive || !_crossPageDisplayEnabled)
                    {
                        return;
                    }
                    _lastCrossPageUpdateUtc = DateTime.UtcNow;
                    UpdateCrossPageDisplay();
                }, DispatcherPriority.Render);
                if (!scheduled)
                {
                    _crossPageUpdatePending = false;
                }
            });
            return;
        }
        _crossPageUpdatePending = true;
        var directScheduled = TryBeginInvoke(() =>
        {
            _crossPageUpdatePending = false;
            if (!_photoModeActive || !_crossPageDisplayEnabled)
            {
                return;
            }
            _lastCrossPageUpdateUtc = DateTime.UtcNow;
            UpdateCrossPageDisplay();
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            _crossPageUpdatePending = false;
        }
    }

    private void ShowPhotoLoadingOverlay(string message)
    {
        _photoLoading = true;
        if (PhotoLoadingText != null)
        {
            PhotoLoadingText.Text = message;
        }
        if (PhotoLoadingOverlay != null)
        {
            PhotoLoadingOverlay.Visibility = Visibility.Visible;
        }
        if (OverlayRoot != null)
        {
            OverlayRoot.IsHitTestVisible = false;
        }
    }

    private void HidePhotoLoadingOverlay()
    {
        _photoLoading = false;
        if (PhotoLoadingOverlay != null)
        {
            PhotoLoadingOverlay.Visibility = Visibility.Collapsed;
        }
        if (OverlayRoot != null)
        {
            OverlayRoot.IsHitTestVisible = _mode != PaintToolMode.Cursor || _photoModeActive;
        }
    }

    private void SchedulePhotoTransformSave(bool userAdjusted)
    {
        if (!_photoModeActive)
        {
            return;
        }
        _photoTransformSavePending = true;
        if (userAdjusted)
        {
            _photoTransformSaveUserAdjusted = true;
        }
        if (_photoTransformSaveTimer == null)
        {
            _photoTransformSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _photoTransformSaveTimer.Tick += (_, _) =>
            {
                _photoTransformSaveTimer?.Stop();
                if (!_photoTransformSavePending)
                {
                    _photoTransformSaveUserAdjusted = false;
                    return;
                }
                var adjusted = _photoTransformSaveUserAdjusted;
                _photoTransformSavePending = false;
                _photoTransformSaveUserAdjusted = false;
                SavePhotoTransformState(adjusted);
            };
        }
        _photoTransformSaveTimer.Stop();
        _photoTransformSaveTimer.Start();
    }

    private void FlushPhotoTransformSave()
    {
        if (!_photoTransformSavePending)
        {
            return;
        }
        _photoTransformSaveTimer?.Stop();
        var adjusted = _photoTransformSaveUserAdjusted;
        _photoTransformSavePending = false;
        _photoTransformSaveUserAdjusted = false;
        SavePhotoTransformState(adjusted);
    }

    private void SchedulePhotoUnifiedTransformSave()
    {
        if (!_photoModeActive || !_crossPageDisplayEnabled)
        {
            return;
        }
        _pendingUnifiedScaleX = _lastPhotoScaleX;
        _pendingUnifiedScaleY = _lastPhotoScaleY;
        _pendingUnifiedTranslateX = _lastPhotoTranslateX;
        _pendingUnifiedTranslateY = _lastPhotoTranslateY;
        if (_photoUnifiedTransformSaveTimer == null)
        {
            _photoUnifiedTransformSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _photoUnifiedTransformSaveTimer.Tick += (_, _) =>
            {
                _photoUnifiedTransformSaveTimer?.Stop();
                PhotoUnifiedTransformChanged?.Invoke(
                    _pendingUnifiedScaleX,
                    _pendingUnifiedScaleY,
                    _pendingUnifiedTranslateX,
                    _pendingUnifiedTranslateY);
            };
        }
        _photoUnifiedTransformSaveTimer.Stop();
        _photoUnifiedTransformSaveTimer.Start();
    }

    private void ResetInkHistory()
    {
        _history.Clear();
        _inkHistory.Clear();
    }

    private void LoadCurrentPageIfExists()
    {
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        if (!_inkCacheEnabled)
        {
            ClearInkSurfaceState();
            return;
        }
        if (string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            return;
        }
        if (_photoCache.TryGet(_currentCacheKey, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"[InkCache] Loaded {cached.Count} strokes for key={_currentCacheKey}");
            ApplyInkStrokes(cached);
            return;
        }
        ClearInkSurfaceState();
    }

    private void ApplyInkStrokes(IReadOnlyList<InkStrokeData> strokes)
    {
        var applySw = Stopwatch.StartNew();
        _inkStrokes.Clear();
        _inkStrokes.AddRange(CloneInkStrokes(strokes));
        RedrawInkSurface();
        _inkCacheDirty = false;
        _perfApplyStrokes.Add(applySw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void SaveCurrentPageIfNeeded()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
    }

    private void MarkInkCacheDirty()
    {
        _inkCacheDirty = true;
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

    private void FinalizeActiveInkOperation()
    {
        if (_lastPointerPosition == null)
        {
            return;
        }
        var position = _lastPointerPosition.Value;
        if (_strokeInProgress)
        {
            EndBrushStroke(position);
        }
        else if (_isErasing)
        {
            EndEraser(position);
        }
        else if (_isRegionSelecting)
        {
            EndRegionSelection(position);
        }
        else if (_isDrawingShape)
        {
            EndShape(position);
        }
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
    }

    private void CopyPhotoBackground(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }
        try
        {
            if (string.Equals(IoPath.GetFullPath(_currentDocumentPath),
                IoPath.GetFullPath(imagePath),
                StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }
        catch
        {
            // Ignore path normalize failures.
        }
        try
        {
            File.Copy(_currentDocumentPath, imagePath, overwrite: true);
        }
        catch
        {
            // Ignore copy exceptions.
        }
    }

    public bool IsWhiteboardActive => IsBoardActive();
    public bool IsPresentationFullscreenActive => _presentationFullscreenActive;

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
