using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// PDF 渲染和缓存管理功能
/// </summary>
public partial class PaintOverlayWindow
{
    #region PDF Loading

    private const long PdfCacheMaxBytes = 100 * 1024 * 1024; // 100MB
    private long _pdfCacheCurrentBytes;

    private void StartPdfOpenAsync(string sourcePath)
    {
        var token = Interlocked.Increment(ref _photoLoadToken);
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            if (!TryOpenPdfDocumentCore(sourcePath, out var document, out var pageCount))
            {
                var scheduled = TryBeginInvoke(() =>
                {
                    if (token != _photoLoadToken)
                    {
                        return;
                    }
                    HidePhotoLoadingOverlay();
                    ExitPhotoMode();
                }, DispatcherPriority.Normal);
                if (!scheduled && document != null)
                {
                    document.Dispose();
                }
                return;
            }
            var openedDocument = document!;
            var scheduledApply = TryBeginInvoke(() =>
            {
                if (token != _photoLoadToken
                    || !_photoModeActive
                    || !_photoDocumentIsPdf
                    || !string.Equals(_currentDocumentPath, sourcePath, StringComparison.OrdinalIgnoreCase))
                {
                    openedDocument.Dispose();
                    return;
                }
                ApplyPdfDocument(openedDocument, pageCount);
                _lastPdfNavigationDirection = 1;
                if (!RenderPdfPage(_currentPageIndex))
                {
                    HidePhotoLoadingOverlay();
                    ExitPhotoMode();
                    return;
                }
                HidePhotoLoadingOverlay();
            }, DispatcherPriority.Render);
            if (!scheduledApply)
            {
                openedDocument.Dispose();
            }
        });
    }

    private static bool TryOpenPdfDocumentCore(string path, out PdfDocumentHost? document, out int pageCount)
    {
        document = null;
        pageCount = 0;
        try
        {
            document = PdfDocumentHost.Open(path);
        }
        catch
        {
            document?.Dispose();
            return false;
        }
        if (document == null)
        {
            return false;
        }
        pageCount = document.PageCount;
        return pageCount > 0;
    }

    private void ApplyPdfDocument(PdfDocumentHost document, int pageCount)
    {
        lock (_pdfRenderLock)
        {
            _pdfDocument = document;
            _pdfPageCount = pageCount;
            _pdfPageCache.Clear();
            _pdfCacheCurrentBytes = 0;
            _pdfPageOrder.Clear();
            _pdfPinnedPages.Clear();
            Interlocked.Exchange(ref _pdfPrefetchInFlight, 0);
            Interlocked.Increment(ref _pdfPrefetchToken);
            Interlocked.Exchange(ref _pdfVisiblePrefetchInFlight, 0);
            Interlocked.Increment(ref _pdfVisiblePrefetchToken);
        }
    }

    private void ClosePdfDocument()
    {
        lock (_pdfRenderLock)
        {
            _pdfDocument?.Dispose();
            _pdfDocument = null;
            _pdfPageCount = 0;
            _pdfPageCache.Clear();
            _pdfCacheCurrentBytes = 0;
            _pdfPageOrder.Clear();
            _pdfPinnedPages.Clear();
            Interlocked.Exchange(ref _pdfPrefetchInFlight, 0);
            Interlocked.Increment(ref _pdfPrefetchToken);
            Interlocked.Exchange(ref _pdfVisiblePrefetchInFlight, 0);
            Interlocked.Increment(ref _pdfVisiblePrefetchToken);
        }
    }

    #endregion

    #region PDF Rendering

    private bool RenderPdfPage(int pageIndex)
    {
        var bitmap = GetPdfPageBitmap(pageIndex);
        if (bitmap == null)
        {
            PhotoBackground.Source = null;
            PhotoBackground.Visibility = Visibility.Collapsed;
            return false;
        }
        PhotoBackground.Source = bitmap;
        PhotoBackground.Visibility = Visibility.Visible;
        UpdateCurrentPageWidthNormalization(bitmap);
        SchedulePdfPrefetch(pageIndex, _lastPdfNavigationDirection);
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

    private bool TryGetCachedPdfPageBitmap(int pageIndex, out BitmapSource? bitmap)
    {
        bitmap = null;
        if (!Monitor.TryEnter(_pdfRenderLock, 50)) // Increased from 2ms to reduce prefetch failures
        {
            return false;
        }
        try
        {
            if (_pdfDocument == null || _pdfPageCount <= 0)
            {
                return false;
            }
            var safeIndex = Math.Clamp(pageIndex, 1, _pdfPageCount);
            if (!_pdfPageCache.TryGetValue(safeIndex, out var cached))
            {
                return false;
            }
            TouchPdfCacheUnsafe(safeIndex);
            bitmap = cached;
            return true;
        }
        finally
        {
            Monitor.Exit(_pdfRenderLock);
        }
    }

    private bool TryGetPdfPageSize(int pageIndex, out System.Windows.Size size)
    {
        size = default;
        if (_pdfDocument == null)
        {
            return false;
        }
        if (!_pdfDocument.TryGetPageSize(pageIndex, out var sizeF))
        {
            return false;
        }
        size = new System.Windows.Size(sizeF.Width * 96.0 / 72.0, sizeF.Height * 96.0 / 72.0);
        return size.Width > 0 && size.Height > 0;
    }

    private double GetScaledPdfPageHeight(int pageIndex)
    {
        if (!_photoDocumentIsPdf)
        {
            return 0;
        }
        if (TryGetCachedPdfPageBitmap(pageIndex, out var cached))
        {
            return GetScaledPageHeight(cached);
        }
        if (TryGetPdfPageSize(pageIndex, out var size))
        {
            return size.Height * _photoScale.ScaleY;
        }
        return 0;
    }

    private BitmapSource? GetPdfPageBitmap(int pageIndex)
    {
        lock (_pdfRenderLock)
        {
            if (_pdfDocument == null || _pdfPageCount <= 0)
            {
                return null;
            }
            var safeIndex = Math.Clamp(pageIndex, 1, _pdfPageCount);
            if (_pdfPageCache.TryGetValue(safeIndex, out var cached))
            {
                TouchPdfCacheUnsafe(safeIndex);
                return cached;
            }
            var rendered = _pdfDocument.RenderPage(safeIndex, PdfDefaultDpi);
            if (rendered == null)
            {
                return null;
            }
            _pdfPageCache[safeIndex] = rendered;
            _pdfCacheCurrentBytes += EstimateBitmapBytes(rendered);
            TouchPdfCacheUnsafe(safeIndex);
            TrimPdfCacheUnsafe();
            return rendered;
        }
    }

    #endregion

    #region PDF Cache Management

    private void TouchPdfCache(int pageIndex)
    {
        lock (_pdfRenderLock)
        {
            TouchPdfCacheUnsafe(pageIndex);
        }
    }

    private void TouchPdfCacheUnsafe(int pageIndex)
    {
        var node = _pdfPageOrder.Find(pageIndex);
        if (node != null)
        {
            _pdfPageOrder.Remove(node);
        }
        _pdfPageOrder.AddLast(pageIndex);
    }

    private void TrimPdfCacheUnsafe()
    {
        while (_pdfPageOrder.Count > PdfCacheLimit || _pdfCacheCurrentBytes > PdfCacheMaxBytes)
        {
            var node = _pdfPageOrder.First;
            while (node != null && _pdfPinnedPages.Contains(node.Value))
            {
                node = node.Next;
            }
            if (node == null)
            {
                break;
            }
            if (_pdfPageCache.TryGetValue(node.Value, out var bitmap))
            {
                _pdfCacheCurrentBytes -= EstimateBitmapBytes(bitmap);
            }
            _pdfPageOrder.Remove(node);
            _pdfPageCache.Remove(node.Value);
        }
        
        System.Diagnostics.Debug.WriteLine($"[PdfCache] Count: {_pdfPageOrder.Count}, Bytes: {_pdfCacheCurrentBytes / 1024 / 1024}MB");
    }

    private static long EstimateBitmapBytes(BitmapSource? bitmap)
    {
        if (bitmap == null) return 0;
        var bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;
        return (long)bitmap.PixelWidth * bitmap.PixelHeight * bytesPerPixel;
    }

    #endregion

    #region PDF Navigation

    private bool TryNavigatePdf(int direction)
    {
        if (!_photoModeActive || !_photoDocumentIsPdf || _pdfDocument == null)
        {
            return false;
        }
        var next = _currentPageIndex + direction;
        if (next < 1 || next > _pdfPageCount)
        {
            return false;  // PDF到边界时返回false，允许跳转到序列中的下一个文件
        }
        SaveCurrentPageOnNavigate(forceBackground: false);
        _currentPageIndex = next;
        _currentCacheKey = BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, isPdf: true);
        ResetInkHistory();
        LoadCurrentPageIfExists();
        _lastPdfNavigationDirection = direction >= 0 ? 1 : -1;
        RenderPdfPage(_currentPageIndex);
        if (_crossPageDisplayEnabled)
        {
            UpdateCrossPageDisplay();
        }
        return true;
    }

    #endregion

    #region PDF Prefetch

    private void SchedulePdfPrefetch(int pageIndex, int direction)
    {
        if (!_photoDocumentIsPdf || _pdfDocument == null || _pdfPageCount <= 0)
        {
            return;
        }
        if (Interlocked.Exchange(ref _pdfPrefetchInFlight, 1) == 1)
        {
            Interlocked.Increment(ref _pdfPrefetchToken);
            return;
        }
        var token = _pdfPrefetchToken;
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            try
            {
                var delay = _crossPageDisplayEnabled ? 0 : 120;
                if (delay > 0)
                {
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                }
                if (token != _pdfPrefetchToken || !_photoModeActive || !_photoDocumentIsPdf)
                {
                    return;
                }
                PrefetchPdfNeighbors(pageIndex, direction, token);
            }
            finally
            {
                Interlocked.Exchange(ref _pdfPrefetchInFlight, 0);
            }
        });
    }

    private void SchedulePdfVisiblePrefetch(IReadOnlyList<int> pageIndexes)
    {
        if (!_photoDocumentIsPdf || _pdfDocument == null || _pdfPageCount <= 0)
        {
            return;
        }
        if (pageIndexes == null || pageIndexes.Count == 0)
        {
            return;
        }
        var unique = pageIndexes
            .Where(p => p >= 1 && p <= _pdfPageCount)
            .Distinct()
            .ToArray();
        if (unique.Length == 0)
        {
            return;
        }
        var token = Interlocked.Increment(ref _pdfVisiblePrefetchToken);
        if (Interlocked.Exchange(ref _pdfVisiblePrefetchInFlight, 1) == 1)
        {
            return;
        }
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                foreach (var pageIndex in unique)
                {
                    if (token != _pdfVisiblePrefetchToken)
                    {
                        return;
                    }
                    if (TryGetCachedPdfPageBitmap(pageIndex, out _))
                    {
                        continue;
                    }
                    GetPdfPageBitmap(pageIndex);
                }
            }
            finally
            {
                Interlocked.Exchange(ref _pdfVisiblePrefetchInFlight, 0);
                if (token == _pdfVisiblePrefetchToken)
                {
                    TryBeginInvoke(() =>
                    {
                        if (_photoModeActive && _photoDocumentIsPdf && _crossPageDisplayEnabled)
                        {
                            UpdateCrossPageDisplay();
                        }
                    }, DispatcherPriority.Background);
                }
            }
        });
    }

    private void PrefetchPdfNeighbors(int pageIndex, int direction, int token)
    {
        var next = pageIndex + 1;
        var prev = pageIndex - 1;
        if (direction < 0)
        {
            if (!PrefetchPdfPage(prev, token))
            {
                PrefetchPdfPage(next, token);
            }
            return;
        }
        if (!PrefetchPdfPage(next, token))
        {
            PrefetchPdfPage(prev, token);
        }
    }

    private bool PrefetchPdfPage(int pageIndex, int token)
    {
        if (pageIndex < 1 || pageIndex > _pdfPageCount)
        {
            return false;
        }
        if (TryGetCachedPdfPageBitmap(pageIndex, out _))
        {
            return true;
        }
        if (!Monitor.TryEnter(_pdfRenderLock, 100)) // Increased from 30ms for background prefetch
        {
            return false;
        }
        try
        {
            if (token != _pdfPrefetchToken || _pdfDocument == null || _pdfPageCount <= 0)
            {
                return false;
            }
            if (_pdfPageCache.ContainsKey(pageIndex))
            {
                TouchPdfCacheUnsafe(pageIndex);
                return true;
            }
            var rendered = _pdfDocument.RenderPage(pageIndex, PdfDefaultDpi);
            if (rendered == null)
            {
                return false;
            }
            _pdfPageCache[pageIndex] = rendered;
            _pdfCacheCurrentBytes += EstimateBitmapBytes(rendered);
            TouchPdfCacheUnsafe(pageIndex);
            TrimPdfCacheUnsafe();
        }
        finally
        {
            Monitor.Exit(_pdfRenderLock);
        }
        return true;
    }

    #endregion
}
