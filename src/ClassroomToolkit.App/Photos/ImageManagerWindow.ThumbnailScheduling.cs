using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
    private const int ThumbnailCacheCapacity = 512;
    private const int ThumbnailDecodeWidthMin = 96;
    private const int ThumbnailDecodeWidthMax = 320;
    private const int ThumbnailVisiblePrefetchRows = 2;
    private const int ThumbnailVisiblePriorityBatchSize = 48;
    private const int ThumbnailBackgroundBatchSize = 24;
    private const int ThumbnailBackgroundQueueIntervalMilliseconds = 90;
    private static readonly int ThumbnailWorkerConcurrency = Math.Clamp(Environment.ProcessorCount / 4, 1, 2);

    private readonly DispatcherTimer _thumbnailBackgroundQueueTimer;
    private readonly Dictionary<string, ThumbnailCacheEntry> _thumbnailCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _thumbnailCacheLru = new();
    private readonly object _thumbnailCacheLock = new();
    private readonly LinkedList<ImageItem> _thumbnailPendingQueue = new();
    private readonly Dictionary<string, LinkedListNode<ImageItem>> _thumbnailPendingNodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _thumbnailPendingLock = new();
    private ScrollViewer? _imageListScrollViewer;

    private sealed class ThumbnailCacheEntry
    {
        public required ImageSource Thumbnail { get; init; }
        public required int PageCount { get; init; }
        public required long ModifiedTicks { get; init; }
        public required LinkedListNode<string> LruNode { get; init; }
    }

    private void OnImageListScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isClosing || ViewModel.ListMode)
        {
            return;
        }

        if (Math.Abs(e.VerticalChange) < double.Epsilon
            && Math.Abs(e.ViewportHeightChange) < double.Epsilon
            && Math.Abs(e.HorizontalChange) < double.Epsilon)
        {
            return;
        }

        QueueVisibleRegionThumbnails();
    }

    private static T? FindDescendant<T>(DependencyObject? root) where T : DependencyObject
    {
        if (root == null)
        {
            return null;
        }

        var childrenCount = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match)
            {
                return match;
            }

            var nested = FindDescendant<T>(child);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    private void EnqueuePendingThumbnail(ImageItem item)
    {
        if (item.IsFolder)
        {
            return;
        }

        lock (_thumbnailPendingLock)
        {
            if (_thumbnailPendingNodes.ContainsKey(item.Path))
            {
                return;
            }

            var node = _thumbnailPendingQueue.AddLast(item);
            _thumbnailPendingNodes[item.Path] = node;
        }
    }

    private bool TryRemovePendingThumbnailByPath(string path, out ImageItem item)
    {
        lock (_thumbnailPendingLock)
        {
            if (!_thumbnailPendingNodes.TryGetValue(path, out var node))
            {
                item = null!;
                return false;
            }

            _thumbnailPendingNodes.Remove(path);
            _thumbnailPendingQueue.Remove(node);
            item = node.Value;
            return true;
        }
    }

    private bool TryDequeuePendingThumbnail(out ImageItem item)
    {
        lock (_thumbnailPendingLock)
        {
            var node = _thumbnailPendingQueue.First;
            if (node == null)
            {
                item = null!;
                return false;
            }

            _thumbnailPendingQueue.RemoveFirst();
            _thumbnailPendingNodes.Remove(node.Value.Path);
            item = node.Value;
            return true;
        }
    }

    private void ResetThumbnailPendingQueue()
    {
        lock (_thumbnailPendingLock)
        {
            _thumbnailPendingQueue.Clear();
            _thumbnailPendingNodes.Clear();
        }
    }

    private bool TryGetActiveThumbnailLoadContext(out CancellationToken token, out int requestId)
    {
        requestId = Volatile.Read(ref _loadImagesRequestId);
        var cts = _thumbnailCts;
        if (cts == null || _isClosing)
        {
            token = default;
            return false;
        }

        try
        {
            token = cts.Token;
        }
        catch (ObjectDisposedException)
        {
            token = default;
            return false;
        }

        return !token.IsCancellationRequested;
    }

    private void QueueVisibleRegionThumbnails()
    {
        if (!TryGetActiveThumbnailLoadContext(out var token, out var requestId))
        {
            return;
        }

        if (ViewModel.ListMode || _isClosing || requestId != Volatile.Read(ref _loadImagesRequestId))
        {
            return;
        }

        _imageListScrollViewer ??= FindDescendant<ScrollViewer>(ImageList);
        var visibleRanges = ResolveVisibleItemRange();
        if (visibleRanges.EndExclusive <= visibleRanges.StartInclusive)
        {
            QueuePendingThumbnailBatch(requestId, ThumbnailVisiblePriorityBatchSize, token);
            EnsureThumbnailBackgroundQueueRunning();
            return;
        }

        var queued = 0;
        for (var index = visibleRanges.StartInclusive; index < visibleRanges.EndExclusive; index++)
        {
            if (queued >= ThumbnailVisiblePriorityBatchSize)
            {
                break;
            }

            if (index < 0 || index >= ViewModel.Images.Count)
            {
                continue;
            }

            var item = ViewModel.Images[index];
            if (item.IsFolder)
            {
                continue;
            }

            if (!TryRemovePendingThumbnailByPath(item.Path, out var pendingItem))
            {
                continue;
            }

            QueueThumbnailLoad(pendingItem, pendingItem.IsPdf, requestId, token);
            queued++;
        }

        if (queued < ThumbnailVisiblePriorityBatchSize)
        {
            QueuePendingThumbnailBatch(requestId, ThumbnailVisiblePriorityBatchSize - queued, token);
        }

        EnsureThumbnailBackgroundQueueRunning();
    }

    private (int StartInclusive, int EndExclusive) ResolveVisibleItemRange()
    {
        var total = ViewModel.Images.Count;
        if (total <= 0)
        {
            return (0, 0);
        }

        var scrollViewer = _imageListScrollViewer;
        if (scrollViewer == null || scrollViewer.ViewportHeight <= 0 || scrollViewer.ViewportWidth <= 0)
        {
            return (0, Math.Min(total, ThumbnailVisiblePriorityBatchSize));
        }

        var tileWidth = (ThumbnailSizeSlider?.Value ?? DefaultThumbnailSize) + 12.0;
        var tileHeight = (ThumbnailSizeSlider?.Value ?? DefaultThumbnailSize) * 0.75 + 42.0;
        var columns = Math.Max(1, (int)Math.Floor(scrollViewer.ViewportWidth / Math.Max(1, tileWidth)));
        var startRow = Math.Max(0, (int)Math.Floor(scrollViewer.VerticalOffset / Math.Max(1, tileHeight)) - ThumbnailVisiblePrefetchRows);
        var visibleRows = (int)Math.Ceiling(scrollViewer.ViewportHeight / Math.Max(1, tileHeight)) + ThumbnailVisiblePrefetchRows * 2;
        var start = startRow * columns;
        var end = Math.Min(total, (startRow + visibleRows) * columns);
        if (end <= start)
        {
            end = Math.Min(total, start + ThumbnailVisiblePriorityBatchSize);
        }

        return (Math.Max(0, start), Math.Max(0, end));
    }

    private void QueuePendingThumbnailBatch(int requestId, int maxCount, CancellationToken token)
    {
        var queued = 0;
        while (queued < maxCount && TryDequeuePendingThumbnail(out var pending))
        {
            QueueThumbnailLoad(pending, pending.IsPdf, requestId, token);
            queued++;
        }
    }

    private void EnsureThumbnailBackgroundQueueRunning()
    {
        if (_isClosing || ViewModel.ListMode || _thumbnailBackgroundQueueTimer.IsEnabled)
        {
            return;
        }

        _thumbnailBackgroundQueueTimer.Start();
    }

    private void OnThumbnailBackgroundQueueTick(object? sender, EventArgs e)
    {
        if (_isClosing || ViewModel.ListMode)
        {
            _thumbnailBackgroundQueueTimer.Stop();
            return;
        }

        if (!TryGetActiveThumbnailLoadContext(out var token, out var requestId))
        {
            _thumbnailBackgroundQueueTimer.Stop();
            return;
        }

        QueuePendingThumbnailBatch(requestId, ThumbnailBackgroundBatchSize, token);
        lock (_thumbnailPendingLock)
        {
            if (_thumbnailPendingQueue.Count == 0)
            {
                _thumbnailBackgroundQueueTimer.Stop();
            }
        }
    }

    private int ResolveThumbnailDecodeWidth()
    {
        var sliderValue = ThumbnailSizeSlider?.Value ?? DefaultThumbnailSize;
        var requested = (int)Math.Round(sliderValue * 0.75);
        return Math.Clamp(requested, ThumbnailDecodeWidthMin, ThumbnailDecodeWidthMax);
    }

    private static string BuildThumbnailCacheKey(string path, bool isPdf, int decodeWidth)
    {
        return isPdf
            ? $"pdf|{path}"
            : $"img|{decodeWidth}|{path}";
    }

    private bool TryGetCachedThumbnail(
        string path,
        bool isPdf,
        int decodeWidth,
        DateTime modified,
        out ImageSource? thumbnail,
        out int pageCount)
    {
        var key = BuildThumbnailCacheKey(path, isPdf, decodeWidth);
        lock (_thumbnailCacheLock)
        {
            if (!_thumbnailCache.TryGetValue(key, out var entry))
            {
                thumbnail = null;
                pageCount = 0;
                return false;
            }

            if (entry.ModifiedTicks != modified.Ticks)
            {
                _thumbnailCache.Remove(key);
                _thumbnailCacheLru.Remove(entry.LruNode);
                thumbnail = null;
                pageCount = 0;
                return false;
            }

            _thumbnailCacheLru.Remove(entry.LruNode);
            _thumbnailCacheLru.AddFirst(entry.LruNode);
            thumbnail = entry.Thumbnail;
            pageCount = entry.PageCount;
            return true;
        }
    }

    private void PutThumbnailCache(
        string path,
        bool isPdf,
        int decodeWidth,
        DateTime modified,
        ImageSource thumbnail,
        int pageCount)
    {
        var key = BuildThumbnailCacheKey(path, isPdf, decodeWidth);
        lock (_thumbnailCacheLock)
        {
            if (_thumbnailCache.TryGetValue(key, out var existing))
            {
                _thumbnailCacheLru.Remove(existing.LruNode);
                _thumbnailCache.Remove(key);
            }

            var node = new LinkedListNode<string>(key);
            _thumbnailCacheLru.AddFirst(node);
            _thumbnailCache[key] = new ThumbnailCacheEntry
            {
                Thumbnail = thumbnail,
                PageCount = pageCount,
                ModifiedTicks = modified.Ticks,
                LruNode = node
            };

            while (_thumbnailCache.Count > ThumbnailCacheCapacity)
            {
                var tail = _thumbnailCacheLru.Last;
                if (tail == null)
                {
                    break;
                }

                _thumbnailCache.Remove(tail.Value);
                _thumbnailCacheLru.RemoveLast();
            }
        }
    }
}
