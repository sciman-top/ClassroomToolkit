using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool IsCrossPageInteractionActive()
    {
        return CrossPageInteractionActivityPolicy.IsActive(
            _photoPanning || _photoManipulating,
            _crossPageDragging,
            IsInkOperationActive())
            || IsPhotoZoomInteractionActive();
    }

    private bool IsCrossPagePanOrDragActive()
    {
        return CrossPageInteractionActivityPolicy.IsActive(
            _photoPanning || _photoManipulating,
            _crossPageDragging,
            inkOperationActive: false);
    }

    private bool IsCrossPageDisplayActive()
    {
        if (Volatile.Read(ref _overlayClosed) != 0)
        {
            return false;
        }

        return PhotoInteractionModePolicy.IsCrossPageDisplayActive(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled());
    }

    private bool IsCrossPageDisplaySettingEnabled()
    {
        return _crossPageDisplayEnabled;
    }

    private bool IsCrossPageImageSequenceActive()
    {
        return IsCrossPageDisplayActive() && !_photoDocumentIsPdf;
    }

    private readonly record struct NeighborInkSlotTag(double BaseTop, double HorizontalOffsetDip);

    private static NeighborInkSlotTag ResolveNeighborInkSlotTag(object? tag, double fallbackBaseTop)
    {
        if (tag is NeighborInkSlotTag slotTag)
        {
            return slotTag;
        }

        if (tag is double baseTop)
        {
            return new NeighborInkSlotTag(baseTop, 0);
        }

        return new NeighborInkSlotTag(fallbackBaseTop, 0);
    }

    private static double ResolveNeighborInkHorizontalOffsetDip(object? tag)
    {
        return tag is NeighborInkSlotTag slotTag
            ? slotTag.HorizontalOffsetDip
            : 0;
    }

    private static void SetNeighborInkSlotTag(WpfImage inkImage, double baseTop, double horizontalOffsetDip)
    {
        var clampedOffset = Math.Max(0, horizontalOffsetDip);
        inkImage.Tag = new NeighborInkSlotTag(baseTop, clampedOffset);
    }

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
        _lastNeighborPagesNonEmptyUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
        _interactiveSwitchPinnedNeighborPage = 0;
        _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    }

    private void ClearNeighborInkVisuals(bool clearSlotIdentity = false, int preservePageIndex = 0)
    {
        var preserveUid = preservePageIndex > 0
            ? preservePageIndex.ToString(CultureInfo.InvariantCulture)
            : string.Empty;
        for (var i = 0; i < _neighborInkImages.Count; i++)
        {
            var inkImg = _neighborInkImages[i];
            if (!string.IsNullOrWhiteSpace(preserveUid)
                && string.Equals(inkImg.Uid, preserveUid, StringComparison.Ordinal))
            {
                inkImg.Visibility = inkImg.Source != null ? Visibility.Visible : Visibility.Collapsed;
                continue;
            }
            TryAssignFrameSource(inkImg, null, forceAssign: true);
            inkImg.Visibility = Visibility.Collapsed;
            if (clearSlotIdentity)
            {
                inkImg.Uid = string.Empty;
                inkImg.Tag = null;
            }
        }

        if (!string.IsNullOrWhiteSpace(preserveUid))
        {
            _interactiveSwitchPinnedNeighborPage = preservePageIndex;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = GetCurrentUtcTimestamp()
                .AddMilliseconds(CrossPageRuntimeDefaults.NeighborPagesClearGraceMs);
        }
        else
        {
            _interactiveSwitchPinnedNeighborPage = 0;
            _interactiveSwitchPinnedNeighborInkHoldUntilUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
        }
    }

    private void ClearNeighborImageCache()
    {
        _neighborImageCache.Clear();
        _neighborInkCache.Clear();
        _neighborPageHeightCache.Clear();
        InvalidateCrossPageBoundsCache();
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
        var bitmap = TryLoadBitmapSource(path, downsampleToMonitor: IsCrossPageImageSequenceActive());
        if (bitmap == null && IsCrossPageImageSequenceActive())
        {
            // Fallback: when downsample decode fails transiently, retry full decode once.
            // This prioritizes continuity over memory in cross-page seam rendering.
            bitmap = TryLoadBitmapSource(path, downsampleToMonitor: false);
            if (bitmap != null)
            {
                _inkDiagnostics?.OnCrossPageUpdateEvent("recover", "neighbor-bitmap-load", $"page={pageIndex} mode=fullres");
            }
        }
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

    private BitmapSource? GetNeighborPageBitmapForRender(int pageIndex, bool allowSynchronousResolve)
    {
        if (!_photoDocumentIsPdf)
        {
            if (!allowSynchronousResolve)
            {
                return _neighborImageCache.TryGetValue(pageIndex, out var cachedImage)
                    ? cachedImage
                    : null;
            }

            return GetPageBitmap(pageIndex);
        }

        if (TryGetCachedPdfPageBitmap(
                pageIndex,
                out var cached,
                tryEnterTimeoutMs: allowSynchronousResolve ? PhotoDocumentRuntimeDefaults.PdfCacheTryEnterTimeoutMs : 0))
        {
            return cached;
        }

        if (!allowSynchronousResolve)
        {
            return null;
        }

        // In cross-page mode, a cache miss can leave a large blank gap between pages.
        // Fallback to direct render once so visible neighbors are always drawable.
        return GetPdfPageBitmap(pageIndex);
    }
    
    private void ScheduleNeighborImagePrefetch(int pageIndex)
    {
        var interactionActiveForPrefetch = IsCrossPagePanOrDragActive();
        if (!CrossPageNeighborPrefetchGatePolicy.ShouldSchedule(
                _photoModeActive,
                _photoDocumentIsPdf,
                IsCrossPageDisplaySettingEnabled(),
                interactionActiveForPrefetch))
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
        var path = _photoSequencePaths[pageIndex - 1];
        var targetDecodeWidth = ResolvePhotoDownsampleDecodeWidth();
        var lifecycleToken = _overlayLifecycleCancellation.Token;
        _ = SafeTaskRunner.Run(
            "PaintOverlayWindow.ScheduleNeighborImagePrefetch",
            cancellationToken =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bitmap = TryLoadBitmapSource(
                path,
                downsampleToMonitor: true,
                targetDecodeWidth);
            if (bitmap == null)
            {
                RemovePendingNeighborImagePrefetch(pageIndex);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var scheduledApply = TryBeginInvoke(() =>
            {
                try
                {
                    if (!CrossPageNeighborPrefetchGatePolicy.ShouldRunPrefetch(
                            _photoModeActive,
                            _photoDocumentIsPdf,
                            IsCrossPageDisplaySettingEnabled(),
                            IsCrossPagePanOrDragActive()))
                    {
                        return;
                    }

                    if (_neighborImageCache.ContainsKey(pageIndex))
                    {
                        return;
                    }

                    if (_photoSequencePaths.Count == 0
                        || pageIndex < 1
                        || pageIndex > _photoSequencePaths.Count
                        || !string.Equals(_photoSequencePaths[pageIndex - 1], path, StringComparison.OrdinalIgnoreCase))
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

                    // Neighbor bitmap became available; request a prompt refresh so boundary
                    // pages can be filled without waiting for the next user input tick.
                    if (IsCrossPageDisplayActive())
                    {
                        RequestCrossPageDisplayUpdate(
                            CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.NeighborRender));
                    }
                }
                finally
                {
                    _neighborImagePrefetchPending.Remove(pageIndex);
                }
            }, DispatcherPriority.Background);
            if (!scheduledApply)
            {
                RemovePendingNeighborImagePrefetch(pageIndex);
                _inkDiagnostics?.OnCrossPageUpdateEvent("defer-abort", "neighbor-prefetch", "dispatch-failed");
            }
        },
            lifecycleToken,
            onError: _ => RemovePendingNeighborImagePrefetch(pageIndex));
    }

    private void RemovePendingNeighborImagePrefetch(int pageIndex)
    {
        var scheduled = TryBeginInvoke(
            () => _neighborImagePrefetchPending.Remove(pageIndex),
            DispatcherPriority.Background);
        if (!scheduled && Dispatcher.CheckAccess())
        {
            _neighborImagePrefetchPending.Remove(pageIndex);
        }
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
        InvalidateCrossPageBoundsCache();
    }

    private void InvalidateCrossPageBoundsCache()
    {
        _crossPageBoundsCacheValid = false;
        _crossPageBoundsCacheUpdatedUtc = CrossPageRuntimeDefaults.UnsetTimestampUtc;
    }

    private bool TryResolveCachedCrossPageBounds(
        bool includeSlack,
        int currentPage,
        int totalPages,
        double viewportWidth,
        double viewportHeight,
        double normalizedWidthDip,
        bool preferCachedDuringInteraction,
        out double minX,
        out double maxX,
        out double minY,
        out double maxY)
    {
        minX = maxX = minY = maxY = 0;
        if (!preferCachedDuringInteraction || !_crossPageBoundsCacheValid)
        {
            return false;
        }

        if (_crossPageBoundsCacheUpdatedUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        var nowUtc = GetCurrentUtcTimestamp();
        if ((nowUtc - _crossPageBoundsCacheUpdatedUtc).TotalMilliseconds > CrossPageBoundsCacheDefaults.InteractiveReuseMaxAgeMs)
        {
            return false;
        }

        const double viewportEpsilon = 0.5;
        if (_crossPageBoundsCacheIncludeSlack != includeSlack
            || _crossPageBoundsCacheCurrentPage != currentPage
            || _crossPageBoundsCacheTotalPages != totalPages
            || Math.Abs(_crossPageBoundsCacheViewportWidth - viewportWidth) > viewportEpsilon
            || Math.Abs(_crossPageBoundsCacheViewportHeight - viewportHeight) > viewportEpsilon
            || Math.Abs(_crossPageBoundsCacheNormalizedWidthDip - normalizedWidthDip) > CrossPageBoundsCacheDefaults.KeyEpsilon
            || Math.Abs(_crossPageBoundsCacheScaleX - _photoScale.ScaleX) > CrossPageBoundsCacheDefaults.KeyEpsilon
            || Math.Abs(_crossPageBoundsCacheScaleY - _photoScale.ScaleY) > CrossPageBoundsCacheDefaults.KeyEpsilon)
        {
            return false;
        }

        minX = _crossPageBoundsCacheMinX;
        maxX = _crossPageBoundsCacheMaxX;
        minY = _crossPageBoundsCacheMinY;
        maxY = _crossPageBoundsCacheMaxY;
        return true;
    }

    private double GetCrossPageNormalizedWidthDip(BitmapSource? fallback = null)
    {
        if (!IsCrossPageImageSequenceActive())
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
        if (!IsCrossPageImageSequenceActive())
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            UpdatePhotoInkClip();
            return;
        }
        var source = bitmap ?? PhotoBackground.Source as BitmapSource;
        if (source == null)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            UpdatePhotoInkClip();
            return;
        }
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(source);
        var pageWidthDip = GetBitmapDisplayWidthInDip(source);
        if (normalizedWidthDip <= 0 || pageWidthDip <= 0)
        {
            _photoPageScale.ScaleX = 1.0;
            _photoPageScale.ScaleY = 1.0;
            UpdatePhotoInkClip();
            return;
        }
        var ratio = normalizedWidthDip / pageWidthDip;
        _photoPageScale.ScaleX = ratio;
        _photoPageScale.ScaleY = ratio;
        UpdatePhotoInkClip();
    }

    private void ApplyNeighborSharedTransform(
        WpfImage pageImage,
        WpfImage inkImage,
        double pageScaleRatio,
        double baseTop)
    {
        var scaleX = _photoScale.ScaleX * pageScaleRatio;
        var scaleY = _photoScale.ScaleY * pageScaleRatio;

        var pageTransform = EnsureNeighborTransform(pageImage);
        pageTransform.Scale.ScaleX = scaleX;
        pageTransform.Scale.ScaleY = scaleY;
        pageTransform.Translate.X = _photoTranslate.X;
        pageTransform.Translate.Y = _photoTranslate.Y + baseTop;

        var inkTag = ResolveNeighborInkSlotTag(inkImage.Tag, baseTop);
        var inkTransform = EnsureNeighborTransform(inkImage);
        inkTransform.Scale.ScaleX = scaleX;
        inkTransform.Scale.ScaleY = scaleY;
        inkTransform.Translate.X = _photoTranslate.X - (inkTag.HorizontalOffsetDip * scaleX);
        inkTransform.Translate.Y = _photoTranslate.Y + inkTag.BaseTop;
    }

    private void ApplyNeighborPageTransform(
        WpfImage pageImage,
        double pageScaleRatio,
        double baseTop)
    {
        var pageTransform = EnsureNeighborTransform(pageImage);
        pageTransform.Scale.ScaleX = _photoScale.ScaleX * pageScaleRatio;
        pageTransform.Scale.ScaleY = _photoScale.ScaleY * pageScaleRatio;
        pageTransform.Translate.X = _photoTranslate.X;
        pageTransform.Translate.Y = _photoTranslate.Y + baseTop;
    }

    private static (TransformGroup Group, ScaleTransform Scale, TranslateTransform Translate) EnsureNeighborTransform(WpfImage image)
    {
        if (image.RenderTransform is TransformGroup existing
            && existing.Children.Count >= 2
            && existing.Children[0] is ScaleTransform existingScale
            && existing.Children[1] is TranslateTransform existingTranslate)
        {
            return (existing, existingScale, existingTranslate);
        }

        var createdScale = new ScaleTransform();
        var createdTranslate = new TranslateTransform();
        var createdGroup = new TransformGroup();
        createdGroup.Children.Add(createdScale);
        createdGroup.Children.Add(createdTranslate);
        image.RenderTransform = createdGroup;
        return (createdGroup, createdScale, createdTranslate);
    }
}
