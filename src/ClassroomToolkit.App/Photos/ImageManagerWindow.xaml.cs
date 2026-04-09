using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow : Window
{
    private const double DefaultLeftRatio = 2.0 / 7.0;
    private const double MinLeftRatio = 0.05;
    private const double MaxLeftRatio = 0.95;
    private const double NarrowWindowThreshold = 1100.0;
    private const double DefaultThumbnailSize = 120.0;
    private const double MinThumbnailSize = 90.0;
    private const double MaxThumbnailSize = 420.0;
    private const double DefaultWindowWidth = 1100.0;
    private const double DefaultWindowHeight = 700.0;
    private const int ThumbnailRefreshDebounceMilliseconds = 90;
    private const int ImageAppendBatchSize = 48;
    private const int FolderNodeRenderBatchSize = 64;

    private IntPtr _hwnd;
    private readonly List<ImageItem> _navigableCache = new();
    private string[] _navigablePathsCache = [];
    private bool _navigableDirty = true;
    private bool _isClosing;
    private CancellationTokenSource? _thumbnailCts;
    private readonly SemaphoreSlim _thumbnailSemaphore = new(ThumbnailWorkerConcurrency);
    private readonly CancellationTokenSource _lifecycleCancellation = new();
    private int _loadImagesRequestId;
    private bool _suppressKeyboardNavigation;
    private bool _layoutApplying;
    private bool _closeCompleted;
    private readonly DispatcherTimer _thumbnailRefreshDebounceTimer;
    private double _preferredLeftRatio = DefaultLeftRatio;
    private int _preferredLeftPanelWidth;
    private double _restoredWindowWidth = DefaultWindowWidth;
    private double _restoredWindowHeight = DefaultWindowHeight;

    public ImageManagerViewModel ViewModel { get; }

    public event Action<IReadOnlyList<string>, int>? ImageSelected;
    public event Action<IReadOnlyList<string>>? FavoritesChanged;
    public event Action<IReadOnlyList<string>>? RecentsChanged;
    public event Action<double, int>? LeftPanelLayoutChanged;

    public ImageManagerWindow(IReadOnlyList<string> favorites, IReadOnlyList<string> recents)
    {
        ViewModel = new ImageManagerViewModel();
        _thumbnailRefreshDebounceTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(ThumbnailRefreshDebounceMilliseconds),
            DispatcherPriority.Background,
            OnThumbnailRefreshDebounceTick,
            Dispatcher.CurrentDispatcher);
        _thumbnailRefreshDebounceTimer.Stop();
        _thumbnailBackgroundQueueTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(ThumbnailBackgroundQueueIntervalMilliseconds),
            DispatcherPriority.Background,
            OnThumbnailBackgroundQueueTick,
            Dispatcher.CurrentDispatcher);
        _thumbnailBackgroundQueueTimer.Stop();
        InitializeComponent();
        DataContext = ViewModel;

        FavoritesList.ItemsSource = ViewModel.Favorites;
        RecentsList.ItemsSource = ViewModel.Recents;
        ImageList.ItemsSource = ViewModel.Images;
        ImageListView.ItemsSource = ViewModel.Images;

        ViewModel.LoadFolderList(ViewModel.Favorites, favorites);
        ViewModel.LoadFolderList(ViewModel.Recents, recents);

        SetViewMode(listMode: false);
        Loaded += OnWindowLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        SourceInitialized += OnWindowSourceInitialized;
    }


    private async Task InitializeTreeAsync(CancellationToken cancellationToken)
    {
        FolderTree.Items.Clear();
        FolderTree.Items.Add(CreateStatusNode("加载中..."));
        try
        {
            var drives = await Task.Run(() => DriveInfo.GetDrives(), cancellationToken);
            var nodes = new List<TreeViewItem>();
            foreach (var drive in drives)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string path;
                string header;
                try
                {
                    path = drive.RootDirectory.FullName;
                    header = drive.Name;
                }
                catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
                {
                    continue;
                }
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }
                nodes.Add(CreateFolderNode(path, header));
            }
            if (_isClosing)
            {
                return;
            }
            FolderTree.Items.Clear();
            if (nodes.Count == 0)
            {
                FolderTree.Items.Add(CreateStatusNode("无可用驱动器"));
                return;
            }
            foreach (var node in nodes)
            {
                FolderTree.Items.Add(node);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested || _isClosing)
        {
            // Window is closing; suppress cancellation noise from background init.
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: InitializeTree Error: {ex}");
            FolderTree.Items.Clear();
            FolderTree.Items.Add(CreateStatusNode("加载失败"));
        }
    }

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
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = new CancellationTokenSource();
        ResetThumbnailPendingQueue();
        _thumbnailBackgroundQueueTimer.Stop();
        var token = _thumbnailCts.Token;
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
            System.Diagnostics.Debug.WriteLine($"ImageManager: LoadImages Error: {ex}");
            if (requestId == Volatile.Read(ref _loadImagesRequestId))
            {
                ShowEmptyState();
            }
        }
    }

    private void ShowEmptyState()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
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

    private void QueueThumbnailLoad(ImageItem item, bool isPdf, CancellationToken token, int requestId)
    {
        if (token.IsCancellationRequested || _isClosing || requestId != Volatile.Read(ref _loadImagesRequestId))
        {
            return;
        }

        var decodeWidth = ResolveThumbnailDecodeWidth();
        if (TryGetCachedThumbnail(
                item.Path,
                isPdf,
                decodeWidth,
                item.Modified,
                out var cachedThumbnail,
                out var cachedPageCount)
            && cachedThumbnail != null)
        {
            _ = TryDispatchThumbnailUpdateAsync(item, cachedThumbnail, cachedPageCount, token, requestId);
            return;
        }

        _ = SafeTaskRunner.Run(
            "ImageManagerWindow.QueueThumbnailLoad",
            async _ =>
            {
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

                if (thumbnail == null || token.IsCancellationRequested || requestId != Volatile.Read(ref _loadImagesRequestId))
                {
                    return;
                }

                PutThumbnailCache(item.Path, isPdf, decodeWidth, item.Modified, thumbnail, pageCount);
                await TryDispatchThumbnailUpdateAsync(item, thumbnail, pageCount, token, requestId);
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
        CancellationToken token,
        int requestId)
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
            }, DispatcherPriority.Background);
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

    public bool TryNavigate(int direction)
    {
        var navigableItems = GetNavigableItems();
        if (navigableItems.Count == 0 || ViewModel.CurrentIndex < 0) return false;
        var next = ViewModel.CurrentIndex + direction;
        if (next < 0 || next >= navigableItems.Count) return false;
        ViewModel.CurrentIndex = next;
        SafeActionExecutionExecutor.TryExecute(
            () => ImageSelected?.Invoke(GetNavigablePaths(), ViewModel.CurrentIndex),
            ex => Debug.WriteLine($"ImageManager: image selected callback failed: {ex.Message}"));
        return true;
    }

    private List<ImageItem> GetNavigableItems()
    {
        if (_navigableDirty)
        {
            _navigableCache.Clear();
            foreach (var item in ViewModel.Images)
            {
                if (!item.IsFolder && (item.IsPdf || item.IsImage))
                {
                    _navigableCache.Add(item);
                }
            }
            var paths = new string[_navigableCache.Count];
            for (var i = 0; i < _navigableCache.Count; i++)
            {
                paths[i] = _navigableCache[i].Path;
            }
            _navigablePathsCache = paths;
            _navigableDirty = false;
        }
        return _navigableCache;
    }

    private IReadOnlyList<string> GetNavigablePaths()
    {
        if (_navigableDirty)
        {
            _ = GetNavigableItems();
        }

        return _navigablePathsCache;
    }

    private static List<string> CreateFolderPathSnapshot(IEnumerable<FolderItem> items)
    {
        var capacity = items switch
        {
            ICollection<FolderItem> collection => collection.Count,
            IReadOnlyCollection<FolderItem> readOnlyCollection => readOnlyCollection.Count,
            _ => 0
        };
        var result = capacity > 0 ? new List<string>(capacity) : new List<string>();
        foreach (var item in items)
        {
            if (item?.Path is string path)
            {
                result.Add(path);
            }
        }

        return result;
    }

}
