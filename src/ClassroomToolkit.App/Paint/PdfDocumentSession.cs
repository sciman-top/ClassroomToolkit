using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Photos;
using WpfSize = System.Windows.Size;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// Owns the current PDF document and its synchronized rendered-page cache.
/// </summary>
internal sealed class PdfDocumentSession : IDisposable
{
    private const long CacheMaxBytes = PhotoDocumentRuntimeDefaults.PdfCacheMaxBytes;

    private readonly object _renderGate = new();
    private readonly Dictionary<int, BitmapSource> _pageCache = new();
    private readonly LinkedList<int> _pageOrder = new();
    private readonly HashSet<int> _pinnedPages = new();
    private IPdfDocumentHost? _document;
    private int _pageCount;
    private long _cacheCurrentBytes;

    public int PageCount => Volatile.Read(ref _pageCount);

    public bool HasDocument => PageCount > 0;

    public void SetDocument(IPdfDocumentHost document, int pageCount)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageCount);

        lock (_renderGate)
        {
            var previousDocument = _document;
            _document = document;
            ClearPageStateUnsafe();
            Volatile.Write(ref _pageCount, pageCount);
            if (!ReferenceEquals(previousDocument, document))
            {
                previousDocument?.Dispose();
            }
        }
    }

    public void Close()
    {
        lock (_renderGate)
        {
            var document = _document;
            _document = null;
            Volatile.Write(ref _pageCount, 0);
            ClearPageStateUnsafe();
            document?.Dispose();
        }
    }

    public bool TryGetCachedPageBitmap(
        int pageIndex,
        out BitmapSource? bitmap,
        int tryEnterTimeoutMs = PhotoDocumentRuntimeDefaults.PdfCacheTryEnterTimeoutMs)
    {
        bitmap = null;
        if (!Monitor.TryEnter(_renderGate, Math.Max(0, tryEnterTimeoutMs)))
        {
            return false;
        }

        try
        {
            if (_document == null || _pageCount <= 0)
            {
                return false;
            }

            var safeIndex = Math.Clamp(pageIndex, 1, _pageCount);
            if (!_pageCache.TryGetValue(safeIndex, out var cached))
            {
                return false;
            }

            TouchPageUnsafe(safeIndex);
            bitmap = cached;
            return true;
        }
        finally
        {
            Monitor.Exit(_renderGate);
        }
    }

    public bool TryGetPageSize(int pageIndex, out WpfSize size)
    {
        size = default;
        if (!Monitor.TryEnter(_renderGate, millisecondsTimeout: 0))
        {
            return false;
        }

        try
        {
            if (_document == null || !_document.TryGetPageSize(pageIndex, out var sizeInPoints))
            {
                return false;
            }

            size = new WpfSize(sizeInPoints.Width * 96.0 / 72.0, sizeInPoints.Height * 96.0 / 72.0);
            return size.Width > 0 && size.Height > 0;
        }
        finally
        {
            Monitor.Exit(_renderGate);
        }
    }

    public BitmapSource? GetPageBitmap(int pageIndex, double dpi)
    {
        lock (_renderGate)
        {
            return GetOrRenderPageUnsafe(pageIndex, dpi);
        }
    }

    public bool TryPrefetchPage(
        int pageIndex,
        double dpi,
        int tryEnterTimeoutMs,
        Func<bool> isRequestCurrent)
    {
        ArgumentNullException.ThrowIfNull(isRequestCurrent);
        if (!Monitor.TryEnter(_renderGate, Math.Max(0, tryEnterTimeoutMs)))
        {
            return false;
        }

        try
        {
            if (!isRequestCurrent())
            {
                return false;
            }

            if (pageIndex < 1 || pageIndex > _pageCount)
            {
                return false;
            }

            return GetOrRenderPageUnsafe(pageIndex, dpi) != null;
        }
        finally
        {
            Monitor.Exit(_renderGate);
        }
    }

    public void SetPinnedPages(IEnumerable<int> pageIndexes)
    {
        ArgumentNullException.ThrowIfNull(pageIndexes);

        lock (_renderGate)
        {
            _pinnedPages.Clear();
            foreach (var pageIndex in pageIndexes)
            {
                if (pageIndex >= 1 && pageIndex <= _pageCount)
                {
                    _pinnedPages.Add(pageIndex);
                }
            }
        }
    }

    public void Dispose() => Close();

    private BitmapSource? GetOrRenderPageUnsafe(int pageIndex, double dpi)
    {
        if (_document == null || _pageCount <= 0)
        {
            return null;
        }

        var safeIndex = Math.Clamp(pageIndex, 1, _pageCount);
        if (_pageCache.TryGetValue(safeIndex, out var cached))
        {
            TouchPageUnsafe(safeIndex);
            return cached;
        }

        var rendered = _document.RenderPage(safeIndex, dpi);
        if (rendered == null)
        {
            return null;
        }

        _pageCache[safeIndex] = rendered;
        _cacheCurrentBytes += EstimateBitmapBytes(rendered);
        TouchPageUnsafe(safeIndex);
        TrimPageCacheUnsafe();
        return rendered;
    }

    private void TouchPageUnsafe(int pageIndex)
    {
        var node = _pageOrder.Find(pageIndex);
        if (node != null)
        {
            _pageOrder.Remove(node);
        }

        _pageOrder.AddLast(pageIndex);
    }

    private void TrimPageCacheUnsafe()
    {
        while (_pageOrder.Count > PhotoDocumentRuntimeDefaults.PdfCacheLimit
            || _cacheCurrentBytes > CacheMaxBytes)
        {
            var node = _pageOrder.First;
            while (node != null && _pinnedPages.Contains(node.Value))
            {
                node = node.Next;
            }

            if (node == null)
            {
                break;
            }

            if (_pageCache.TryGetValue(node.Value, out var bitmap))
            {
                _cacheCurrentBytes -= EstimateBitmapBytes(bitmap);
            }

            _pageOrder.Remove(node);
            _pageCache.Remove(node.Value);
        }

        Debug.WriteLine($"[PdfCache] Count: {_pageOrder.Count}, Bytes: {_cacheCurrentBytes / 1024 / 1024}MB");
    }

    private void ClearPageStateUnsafe()
    {
        _pageCache.Clear();
        _pageOrder.Clear();
        _pinnedPages.Clear();
        _cacheCurrentBytes = 0;
    }

    private static long EstimateBitmapBytes(BitmapSource bitmap)
    {
        var bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;
        return (long)bitmap.PixelWidth * bitmap.PixelHeight * bytesPerPixel;
    }
}
