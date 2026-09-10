using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Utilities;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
    private void OnThumbnailSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ViewModel is null || ViewModel.ListMode)
        {
            return;
        }

        _thumbnailRefreshDebounceTimer.Stop();
        _thumbnailRefreshDebounceTimer.Start();
    }

    private void OnThumbnailRefreshDebounceTick(object? sender, EventArgs e)
    {
        _thumbnailRefreshDebounceTimer.Stop();
        if (ViewModel is null || ViewModel.ListMode || _isClosing)
        {
            return;
        }

        ImageList?.Items.Refresh();
        QueueVisibleRegionThumbnails();
    }

    private void StartLoadImages(string folder)
    {
        var requestId = Interlocked.Increment(ref _loadImagesRequestId);
        _ = LoadImagesAsync(folder, requestId);
    }

    private async Task LoadImagesAsync(string folder, int requestId)
    {
        var nextThumbnailCts = new CancellationTokenSource();
        var previousThumbnailCts = Interlocked.Exchange(ref _thumbnailCts, nextThumbnailCts);
        if (previousThumbnailCts != null)
        {
            try
            {
                await previousThumbnailCts.CancelAsync();
            }
            catch (ObjectDisposedException)
            {
                // Token source may be disposed during rapid folder switching or shutdown.
            }
            catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Debug.WriteLine($"ImageManager: previous thumbnail cancellation failed: {ex.Message}");
            }
            previousThumbnailCts.Dispose();
        }

        ResetThumbnailPendingQueue();
        _thumbnailBackgroundQueueTimer.Stop();
        var token = nextThumbnailCts.Token;
        ViewModel.Images.Clear();
        _navigableDirty = true;
        EmptyHintText.Visibility = Visibility.Collapsed;

        try
        {
            var loadResult = await Task.Run(() =>
            {
                var cleanupSummary = CleanupOrphanInkArtifacts(folder);
                var scanResult = ScanDirectory(folder, token);
                return (cleanupSummary, scanResult);
            }, token);
            var result = loadResult.scanResult;
            if (result == null || token.IsCancellationRequested || requestId != Volatile.Read(ref _loadImagesRequestId))
            {
                return;
            }
            if (loadResult.cleanupSummary.SidecarsDeleted > 0 || loadResult.cleanupSummary.CompositesDeleted > 0)
            {
                Debug.WriteLine(
                    $"[ImageManager] orphan-ink-cleanup folder={folder} sidecars={loadResult.cleanupSummary.SidecarsDeleted} composites={loadResult.cleanupSummary.CompositesDeleted}");
            }

            await AppendScanResultsAsync(result, token, requestId);
            if (token.IsCancellationRequested
                || requestId != Volatile.Read(ref _loadImagesRequestId)
                || _isClosing)
            {
                return;
            }

            ViewModel.CurrentIndex = GetNavigableItems().Count > 0 ? 0 : -1;
            EmptyHintText.Visibility = ViewModel.Images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested || _isClosing) { }
        catch (ObjectDisposedException)
        {
            // Token/dispatcher resources may be disposed during rapid window shutdown.
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: LoadImages Error: {ex}");
            if (requestId == Volatile.Read(ref _loadImagesRequestId))
            {
                ShowEmptyState();
            }
        }
    }

    private void ShowEmptyState()
    {
        TryCancelAndDisposeThumbnailCts();
        _thumbnailBackgroundQueueTimer.Stop();
        ResetThumbnailPendingQueue();
        ViewModel.CurrentFolder = string.Empty;
        CurrentFolderText.Text = "此电脑";
        ViewModel.Images.Clear();
        _navigableDirty = true;
        ViewModel.CurrentIndex = -1;
        EmptyHintText.Visibility = Visibility.Visible;
        ViewModel.UpdateNavigationStates();
    }

    private void QueueThumbnailLoad(ImageItem item, bool isPdf, int requestId, CancellationToken token)
    {
        if (token.IsCancellationRequested || _isClosing || requestId != Volatile.Read(ref _loadImagesRequestId))
        {
            return;
        }

        var decodeWidth = ResolveThumbnailDecodeWidth();
        _ = SafeTaskRunner.Run(
            "ImageManagerWindow.QueueThumbnailLoad",
            async _ =>
            {
                if (TryGetCachedThumbnail(
                        item.Path,
                        isPdf,
                        decodeWidth,
                        out var cachedThumbnail,
                        out var cachedPageCount)
                    && cachedThumbnail != null)
                {
                    await TryDispatchThumbnailUpdateAsync(item, cachedThumbnail, cachedPageCount, requestId, token);
                    return;
                }

                try { await _thumbnailSemaphore.WaitAsync(token); }
                catch (OperationCanceledException) { return; }
                catch (ObjectDisposedException) { return; }

                if (token.IsCancellationRequested || _isClosing)
                {
                    TryReleaseThumbnailSemaphore();
                    return;
                }

                ImageSource? thumbnail = null;
                int pageCount = item.PageCount;
                try
                {
                    if (isPdf)
                    {
                        var preview = LoadPdfPreview(item.Path);
                        thumbnail = preview.Thumbnail;
                        pageCount = preview.PageCount;
                    }
                    else
                    {
                        thumbnail = LoadThumbnail(item.Path, decodeWidth);
                    }
                }
                finally { TryReleaseThumbnailSemaphore(); }

                PhotoFileFingerprint? fingerprint = null;
                if (thumbnail != null
                    && PhotoFileFingerprintReader.TryRead(item.Path, out var currentFingerprint))
                {
                    fingerprint = currentFingerprint;
                }

                if (thumbnail != null && fingerprint.HasValue && !_isClosing)
                {
                    PutThumbnailCache(item.Path, isPdf, decodeWidth, fingerprint.Value, thumbnail, pageCount);
                }

                if (thumbnail == null || token.IsCancellationRequested || requestId != Volatile.Read(ref _loadImagesRequestId))
                {
                    return;
                }

                await TryDispatchThumbnailUpdateAsync(item, thumbnail, pageCount, requestId, token);
            },
            token,
            ex => Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatThumbnailDispatchFailureMessage(
                    item.Path,
                    ex.Message)));
    }

    private async Task TryDispatchThumbnailUpdateAsync(
        ImageItem item,
        ImageSource thumbnail,
        int pageCount,
        int requestId,
        CancellationToken token)
    {
        try
        {
            if (Dispatcher.CheckAccess())
            {
                if (!_isClosing && !token.IsCancellationRequested && requestId == Volatile.Read(ref _loadImagesRequestId))
                {
                    item.Thumbnail = thumbnail;
                    if (item.IsPdf && pageCount > 0)
                    {
                        item.PageCount = pageCount;
                    }
                }
                return;
            }
            if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (!_isClosing && !token.IsCancellationRequested && requestId == Volatile.Read(ref _loadImagesRequestId))
                {
                    item.Thumbnail = thumbnail;
                    if (item.IsPdf && pageCount > 0)
                    {
                        item.PageCount = pageCount;
                    }
                }
            }, DispatcherPriority.Background, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            if (Dispatcher.CheckAccess()
                && !_isClosing
                && !token.IsCancellationRequested
                && requestId == Volatile.Read(ref _loadImagesRequestId))
            {
                item.Thumbnail = thumbnail;
                if (item.IsPdf && pageCount > 0)
                {
                    item.PageCount = pageCount;
                }
                return;
            }
            Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatThumbnailDispatchFailureMessage(
                    item.Path,
                    ex.Message));
        }
    }
}
