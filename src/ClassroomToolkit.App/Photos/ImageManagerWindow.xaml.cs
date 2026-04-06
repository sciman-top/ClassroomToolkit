using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.App.Settings;
using WpfListViewItem = System.Windows.Controls.ListViewItem;

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

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        _imageListScrollViewer = FindDescendant<ScrollViewer>(ImageList);
        _ = InitializeTreeAsync(_lifecycleCancellation.Token);
        InitializeDefaultFolder();
        EnterInitialMaximizedState();
        ApplyAdaptiveLayout();
        UpdateWindowStateToggleButton();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        BeginClose();
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        CompleteClose();
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        TrackRestoredWindowSize();
        ApplyAdaptiveLayout();
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        ApplyAdaptiveLayout();
        UpdateWindowStateToggleButton();
    }

    private void OnWindowSourceInitialized(object? sender, EventArgs e)
    {
        _hwnd = new WindowInteropHelper(this).Handle;
        RemoveMinimizeButton();
    }

    public void SetKeyboardNavigationSuppressed(bool suppressed)
    {
        _suppressKeyboardNavigation = suppressed;
    }

    public void ApplyLayoutSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (settings.PhotoManagerWindowWidth > 0)
        {
            Width = settings.PhotoManagerWindowWidth;
            _restoredWindowWidth = settings.PhotoManagerWindowWidth;
        }
        if (settings.PhotoManagerWindowHeight > 0)
        {
            Height = settings.PhotoManagerWindowHeight;
            _restoredWindowHeight = settings.PhotoManagerWindowHeight;
        }

        _preferredLeftRatio = NormalizeLeftRatio(settings.PhotoManagerLeftPanelRatio, DefaultLeftRatio);
        _preferredLeftPanelWidth = Math.Max(0, settings.PhotoManagerLeftPanelWidth);
        if (ThumbnailSizeSlider != null)
        {
            ThumbnailSizeSlider.Value = NormalizeThumbnailSize(settings.PhotoManagerThumbnailSize, DefaultThumbnailSize);
        }
        SetViewMode(settings.PhotoManagerListMode);
    }

    public void CaptureLayoutSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        UpdatePreferredLeftLayoutFromCurrent();
        var width = WindowState == WindowState.Normal
            ? (ActualWidth > 0 ? ActualWidth : Width)
            : _restoredWindowWidth;
        var height = WindowState == WindowState.Normal
            ? (ActualHeight > 0 ? ActualHeight : Height)
            : _restoredWindowHeight;
        settings.PhotoManagerWindowWidth = (int)Math.Round(width);
        settings.PhotoManagerWindowHeight = (int)Math.Round(height);
        settings.PhotoManagerLeftPanelRatio = _preferredLeftRatio;
        settings.PhotoManagerLeftPanelWidth = _preferredLeftPanelWidth;
        settings.PhotoManagerThumbnailSize = NormalizeThumbnailSize(ThumbnailSizeSlider?.Value ?? DefaultThumbnailSize, DefaultThumbnailSize);
        settings.PhotoManagerListMode = ViewModel.ListMode;
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

    private void InitializeDefaultFolder()
    {
        foreach (var candidate in EnumerateDefaultFolderCandidates())
        {
            if (TryOpenDefaultFolder(candidate))
            {
                return;
            }
        }

        ShowEmptyState();
    }

    private void OnAddFavoriteClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择要添加到收藏夹的目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        System.Windows.Forms.DialogResult result;
        try
        {
            result = dialog.ShowDialog();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatFavoriteFolderDialogFailureMessage(
                    ex.Message));
            return;
        }
        if (result != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }
        AddFavorite(dialog.SelectedPath);
    }

    private void OnRemoveFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (FavoritesList.SelectedItem is not FolderItem selected)
        {
            return;
        }
        RemoveFavorite(selected.Path, keepInRecents: true);
    }

    private void OnFavoritesSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FavoritesList.SelectedItem is FolderItem item)
        {
            OpenFolder(item.Path);
        }
    }

    private void OnRecentsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RecentsList.SelectedItem is FolderItem item)
        {
            OpenFolder(item.Path);
        }
    }

    private void OnClearRecentsClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Recents.Count == 0)
        {
            return;
        }

        ViewModel.Recents.Clear();
        SafeActionExecutionExecutor.TryExecute(
            () => RecentsChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Recents)),
            ex => Debug.WriteLine($"ImageManager: recents callback failed: {ex.Message}"));
    }

    private void OnFavoriteStarToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string path || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        RemoveFavorite(path, keepInRecents: true);
    }

    private void OnRecentStarToggleClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string path || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        AddFavorite(path);
    }

    private void OnFolderTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item)
        {
            return;
        }
        if (item.Tag is not string path || string.IsNullOrWhiteSpace(path))
        {
            ShowEmptyState();
            return;
        }
        OpenFolder(path);
    }

    private void OnImageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var item = ResolveSelectedImageItem(sender, e);
        if (item == null || item.IsFolder)
        {
            return;
        }

        var navigableItems = GetNavigableItems();
        ViewModel.CurrentIndex = navigableItems.IndexOf(item);
        if (ViewModel.CurrentIndex < 0)
        {
            ViewModel.CurrentIndex = navigableItems.FindIndex(image =>
                string.Equals(image.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        }
        if (ViewModel.CurrentIndex >= 0)
        {
            SafeActionExecutionExecutor.TryExecute(
                () => ImageSelected?.Invoke(GetNavigablePaths(), ViewModel.CurrentIndex),
                ex => Debug.WriteLine($"ImageManager: image selected callback failed: {ex.Message}"));
        }
    }

    private static ImageItem? ResolveSelectedImageItem(object sender, SelectionChangedEventArgs e)
    {
        if (sender is Selector selector && selector.SelectedItem is ImageItem selected)
        {
            return selected;
        }
        if (e.AddedItems.Count > 0 && e.AddedItems[0] is ImageItem added)
        {
            return added;
        }
        return null;
    }

    private void OnImageListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not WpfListViewItem)
        {
            dep = VisualTreeHelper.GetParent(dep);
        }
        if (dep is not WpfListViewItem lvi || lvi.DataContext is not ImageItem item)
        {
            return;
        }
        if (item.IsFolder && Directory.Exists(item.Path))
        {
            OpenFolder(item.Path);
            e.Handled = true;
        }
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (ReferenceEquals(Keyboard.FocusedElement, CurrentFolderText))
        {
            return;
        }
        if (!_suppressKeyboardNavigation)
        {
            return;
        }
        if (!IsPhotoNavigationKey(e.Key))
        {
            return;
        }
        e.Handled = true;
    }

    private void OnCurrentFolderTextKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        var raw = CurrentFolderText.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            ShowEmptyState();
            e.Handled = true;
            return;
        }

        if (TryResolveExistingFolder(raw, out var folder))
        {
            OpenFolder(folder, addToRecents: true);
            e.Handled = true;
            return;
        }

        CurrentFolderText.SelectAll();
        e.Handled = true;
    }

    private static bool IsPhotoNavigationKey(Key key)
    {
        return key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down ||
               key == Key.PageUp || key == Key.PageDown || key == Key.Space || key == Key.Enter;
    }

    private void OnViewModeClick(object sender, RoutedEventArgs e)
    {
        if (ReferenceEquals(sender, ThumbnailViewButton))
        {
            SetViewMode(listMode: false);
            return;
        }
        if (ReferenceEquals(sender, ListViewButton))
        {
            SetViewMode(listMode: true);
        }
    }

    private void OnBackClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.BackStack.Count == 0)
        {
            return;
        }
        if (!string.IsNullOrEmpty(ViewModel.CurrentFolder))
        {
            ViewModel.ForwardStack.Add(ViewModel.CurrentFolder);
        }
        var previousFolder = ViewModel.BackStack[^1];
        ViewModel.BackStack.RemoveAt(ViewModel.BackStack.Count - 1);
        OpenFolder(previousFolder, addToRecents: false, navigate: false);
    }

    private void OnForwardClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel.ForwardStack.Count == 0)
        {
            return;
        }
        var nextFolder = ViewModel.ForwardStack[^1];
        ViewModel.ForwardStack.RemoveAt(ViewModel.ForwardStack.Count - 1);
        if (!string.IsNullOrEmpty(ViewModel.CurrentFolder))
        {
            ViewModel.BackStack.Add(ViewModel.CurrentFolder);
        }
        OpenFolder(nextFolder, addToRecents: true, navigate: false);
    }

    private void OnUpClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(ViewModel.CurrentFolder))
        {
            return;
        }
        try
        {
            var parentDir = Directory.GetParent(ViewModel.CurrentFolder);
            if (parentDir != null && parentDir.Exists)
            {
                OpenFolder(parentDir.FullName, addToRecents: true);
            }
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatUpNavigationFailureMessage(
                    ViewModel.CurrentFolder,
                    ex.Message));
        }
    }

    private void SetViewMode(bool listMode)
    {
        ViewModel.ListMode = listMode;
        ThumbnailViewButton.IsChecked = !listMode;
        ListViewButton.IsChecked = listMode;
        ImageList.Visibility = listMode ? Visibility.Collapsed : Visibility.Visible;
        ImageListView.Visibility = listMode ? Visibility.Visible : Visibility.Collapsed;
        if (ThumbnailSizeSlider != null)
        {
            ThumbnailSizeSlider.IsEnabled = !listMode;
        }

        if (listMode)
        {
            _thumbnailBackgroundQueueTimer.Stop();
            return;
        }

        QueueVisibleRegionThumbnails();
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

    private void AddFavorite(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return;
        }
        if (ViewModel.Favorites.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        ViewModel.Favorites.Insert(0, new FolderItem(path));
        var existingRecent = ViewModel.Recents.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existingRecent != null)
        {
            ViewModel.Recents.Remove(existingRecent);
            SafeActionExecutionExecutor.TryExecute(
                () => RecentsChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Recents)),
                ex => Debug.WriteLine($"ImageManager: recents callback failed: {ex.Message}"));
        }
        SafeActionExecutionExecutor.TryExecute(
            () => FavoritesChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Favorites)),
            ex => Debug.WriteLine($"ImageManager: favorites callback failed: {ex.Message}"));
    }

    private void RemoveFavorite(string path, bool keepInRecents)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var favorite = ViewModel.Favorites.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (favorite == null)
        {
            return;
        }

        ViewModel.Favorites.Remove(favorite);
        SafeActionExecutionExecutor.TryExecute(
            () => FavoritesChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Favorites)),
            ex => Debug.WriteLine($"ImageManager: favorites callback failed: {ex.Message}"));

        if (keepInRecents)
        {
            UpdateRecents(path);
        }
    }

    private void OpenFolder(string path, bool addToRecents = true, bool navigate = true)
    {
        if (!TryResolveExistingFolder(path, out var folder))
        {
            ShowEmptyState();
            return;
        }

        if (string.Equals(ViewModel.CurrentFolder, folder, StringComparison.OrdinalIgnoreCase))
        {
            if (addToRecents)
            {
                UpdateRecents(folder);
            }
            ViewModel.UpdateNavigationStates();
            return;
        }

        if (navigate && !string.IsNullOrEmpty(ViewModel.CurrentFolder) && !string.Equals(ViewModel.CurrentFolder, folder, StringComparison.OrdinalIgnoreCase))
        {
            ViewModel.BackStack.Add(ViewModel.CurrentFolder);
            ViewModel.ForwardStack.Clear();
        }
        ViewModel.CurrentFolder = folder;
        CurrentFolderText.Text = folder;
        StartLoadImages(folder);
        ViewModel.UpdateNavigationStates();
        if (addToRecents)
        {
            UpdateRecents(folder);
        }
    }

    private IEnumerable<string> EnumerateDefaultFolderCandidates()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in ViewModel.Recents)
        {
            if (item?.Path is null || !seen.Add(item.Path))
            {
                continue;
            }
            yield return item.Path;
        }

        foreach (var item in ViewModel.Favorites)
        {
            if (item?.Path is null || !seen.Add(item.Path))
            {
                continue;
            }
            yield return item.Path;
        }

        foreach (var folder in GetSystemDefaultFolders())
        {
            if (seen.Add(folder))
            {
                yield return folder;
            }
        }
    }

    private static IEnumerable<string> GetSystemDefaultFolders()
    {
        var candidates = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                yield return candidate;
            }
        }
    }

    private bool TryOpenDefaultFolder(string path)
    {
        if (!TryResolveExistingFolder(path, out var folder))
        {
            return false;
        }

        OpenFolder(folder, addToRecents: false, navigate: false);
        return true;
    }

    private static bool TryResolveExistingFolder(string path, out string folder)
    {
        folder = string.Empty;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var normalized = path.Trim().Trim('"');
        if (normalized.Length == 0)
        {
            return false;
        }

        if (normalized.Length == 2
            && char.IsLetter(normalized[0])
            && normalized[1] == ':')
        {
            normalized += "\\";
        }

        if (Directory.Exists(normalized))
        {
            folder = normalized;
            return true;
        }

        if (!File.Exists(normalized))
        {
            return false;
        }

        var parent = Path.GetDirectoryName(normalized);
        if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
        {
            return false;
        }

        folder = parent;
        return true;
    }

    private void UpdateRecents(string path)
    {
        if (ViewModel.Recents.Count > 0 &&
            string.Equals(ViewModel.Recents[0].Path, path, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var existing = ViewModel.Recents.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            ViewModel.Recents.Remove(existing);
        }
        ViewModel.Recents.Insert(0, new FolderItem(path));
        while (ViewModel.Recents.Count > 10)
        {
            ViewModel.Recents.RemoveAt(ViewModel.Recents.Count - 1);
        }
        SafeActionExecutionExecutor.TryExecute(
            () => RecentsChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Recents)),
            ex => Debug.WriteLine($"ImageManager: recents callback failed: {ex.Message}"));
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

    private void BeginClose()
    {
        if (_isClosing) return;
        _isClosing = true;
        _lifecycleCancellation.Cancel();
        UpdatePreferredLeftLayoutFromCurrent();
        SafeActionExecutionExecutor.TryExecute(
            () => LeftPanelLayoutChanged?.Invoke(_preferredLeftRatio, _preferredLeftPanelWidth),
            ex => Debug.WriteLine($"ImageManager: close layout callback failed: {ex.Message}"));
        _thumbnailCts?.Cancel();
    }

    private void CompleteClose()
    {
        if (_closeCompleted)
        {
            return;
        }

        _closeCompleted = true;
        Loaded -= OnWindowLoaded;
        PreviewKeyDown -= OnPreviewKeyDown;
        Closing -= OnWindowClosing;
        Closed -= OnWindowClosed;
        SizeChanged -= OnWindowSizeChanged;
        StateChanged -= OnWindowStateChanged;
        SourceInitialized -= OnWindowSourceInitialized;
        _thumbnailRefreshDebounceTimer.Stop();
        _thumbnailRefreshDebounceTimer.Tick -= OnThumbnailRefreshDebounceTick;
        _thumbnailBackgroundQueueTimer.Stop();
        _thumbnailBackgroundQueueTimer.Tick -= OnThumbnailBackgroundQueueTick;
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
        ResetThumbnailPendingQueue();
        lock (_thumbnailCacheLock)
        {
            _thumbnailCache.Clear();
            _thumbnailCacheLru.Clear();
        }
        try
        {
            _thumbnailSemaphore.Dispose();
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: thumbnail semaphore dispose failed: {ex.Message}");
        }
        _lifecycleCancellation.Dispose();
    }

    private void TryReleaseThumbnailSemaphore()
    {
        try
        {
            _thumbnailSemaphore.Release();
        }
        catch (ObjectDisposedException)
        {
            // Expected when background workers race with window shutdown.
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

    private static TreeViewItem CreateFolderNode(string path, string header)
    {
        var item = new TreeViewItem { Header = CreateVisibleTreeHeader(header), Tag = path };
        item.Items.Add(CreateStatusNode("\u52a0\u8f7d\u4e2d..."));
        item.Expanded += OnFolderExpanded;
        return item;
    }

    private static void OnFolderExpanded(object sender, RoutedEventArgs e)
    {
        _ = OnFolderExpandedAsync(sender);
    }

    private static async Task OnFolderExpandedAsync(object sender)
    {
        var path = "unknown";
        try
        {
            if (sender is not TreeViewItem item || item.Items.Count != 1 || item.Items[0] is not TreeViewItem placeholder)
            {
                return;
            }

            var placeholderText = ResolveTreeHeaderText(placeholder.Header);
            if (!placeholderText.Contains("\u52a0\u8f7d\u4e2d", StringComparison.Ordinal))
            {
                return;
            }

            item.Items.Clear();
            if (item.Tag is not string folderPath || string.IsNullOrWhiteSpace(folderPath))
            {
                return;
            }
            path = folderPath;

            var directories = await Task.Run(() =>
            {
                var result = new List<string>();
                foreach (var dir in Directory.EnumerateDirectories(folderPath, "*", TopLevelIgnoreInaccessibleOptions))
                {
                    var name = Path.GetFileName(dir);
                    if (string.IsNullOrWhiteSpace(name) || name.StartsWith(".", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (IsHiddenFile(dir))
                    {
                        continue;
                    }

                    result.Add(dir);
                }

                result.Sort(StringComparer.OrdinalIgnoreCase);
                return result;
            });

            for (var i = 0; i < directories.Count; i += FolderNodeRenderBatchSize)
            {
                var upperBound = Math.Min(i + FolderNodeRenderBatchSize, directories.Count);
                for (var j = i; j < upperBound; j++)
                {
                    var dir = directories[j];
                    var name = Path.GetFileName(dir);
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        item.Items.Add(CreateFolderNode(dir, name));
                    }
                }

                if (upperBound < directories.Count)
                {
                    if (item.Dispatcher.HasShutdownStarted || item.Dispatcher.HasShutdownFinished)
                    {
                        return;
                    }
                    await item.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Dispatcher/task cancellation can happen when tree view shuts down during async expansion.
        }
        catch (ObjectDisposedException)
        {
            // Tree view/window disposed during async expansion.
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatFolderExpandFailureMessage(
                    path,
                    ex.Message));
        }
    }

    private async Task AppendScanResultsAsync(
        IReadOnlyList<ImageItem> result,
        CancellationToken token,
        int requestId)
    {
        for (var i = 0; i < result.Count; i += ImageAppendBatchSize)
        {
            if (token.IsCancellationRequested || requestId != Volatile.Read(ref _loadImagesRequestId) || _isClosing)
            {
                return;
            }

            var upperBound = Math.Min(i + ImageAppendBatchSize, result.Count);
            for (var j = i; j < upperBound; j++)
            {
                var item = result[j];
                ViewModel.Images.Add(item);
                if (!item.IsFolder)
                {
                    EnqueuePendingThumbnail(item);
                }
            }

            QueueVisibleRegionThumbnails();

            if (upperBound < result.Count)
            {
                if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished || _isClosing)
                {
                    return;
                }
                try
                {
                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Background);
                }
                catch (OperationCanceledException) when (_isClosing)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        QueueVisibleRegionThumbnails();
    }

    private void RemoveMinimizeButton()
    {
        var hwnd = _hwnd != IntPtr.Zero ? _hwnd : new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        if (!WindowStyleExecutor.TryUpdateStyleBits(
                hwnd,
                WindowStyleBitMasks.GwlStyle,
                setMask: 0,
                clearMask: WindowStyleBitMasks.WsMinimizeBox,
                out var style))
        {
            return;
        }
        if ((style & WindowStyleBitMasks.WsMinimizeBox) != 0) return;
        WindowPlacementExecutor.TryRefreshFrame(hwnd);
    }

    private static TreeViewItem CreateStatusNode(string text)
    {
        return new TreeViewItem
        {
            Header = CreateVisibleTreeHeader(text),
            IsHitTestVisible = false
        };
    }

    private static TextBlock CreateVisibleTreeHeader(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = System.Windows.Application.Current?.TryFindResource("Brush_Text_Primary") as System.Windows.Media.Brush
                ?? System.Windows.Media.Brushes.White,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
    }

    private static string ResolveTreeHeaderText(object? header)
    {
        return header switch
        {
            TextBlock textBlock => textBlock.Text ?? string.Empty,
            string text => text,
            _ => string.Empty
        };
    }

    private void OnMainColumnSplitterDragDelta(object sender, DragDeltaEventArgs e)
    {
        UpdatePreferredLeftLayoutFromCurrent();
    }

    private void OnMainColumnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (_isClosing || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }
        void ApplyDeferredSplitterUpdate()
        {
            UpdatePreferredLeftLayoutFromCurrent();
            SafeActionExecutionExecutor.TryExecute(
                () => LeftPanelLayoutChanged?.Invoke(_preferredLeftRatio, _preferredLeftPanelWidth),
                ex => Debug.WriteLine($"ImageManager: layout callback failed: {ex.Message}"));
        }

        var scheduled = false;
        try
        {
            Dispatcher.BeginInvoke(
                new Action(ApplyDeferredSplitterUpdate),
                DispatcherPriority.Background);
            scheduled = true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: deferred splitter update dispatch failed: {ex.GetType().Name} - {ex.Message}");
        }
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ApplyDeferredSplitterUpdate();
        }
    }

    private void ApplyAdaptiveLayout()
    {
        if (_layoutApplying || !IsLoaded || RootGrid == null)
        {
            return;
        }

        var totalWidth = RootGrid.ActualWidth;
        if (totalWidth <= 0)
        {
            return;
        }

        _layoutApplying = true;
        try
        {
            var splitterWidth = Math.Max(0, SplitterColumn.ActualWidth > 0 ? SplitterColumn.ActualWidth : 6);
            var available = Math.Max(1, totalWidth - splitterWidth);

            var effectiveRatio = _preferredLeftRatio;
            if (ActualWidth > 0 && ActualWidth < NarrowWindowThreshold)
            {
                effectiveRatio = Math.Min(effectiveRatio, 0.27);
            }

            var minRight = RightColumn.MinWidth > 0 ? RightColumn.MinWidth : 560.0;
            var desiredLeft = _preferredLeftPanelWidth > 0
                ? _preferredLeftPanelWidth
                : available * effectiveRatio;
            var maxLeft = double.IsInfinity(LeftColumn.MaxWidth) ? available - minRight : LeftColumn.MaxWidth;
            maxLeft = Math.Max(LeftColumn.MinWidth, Math.Min(maxLeft, available - minRight));
            var boundedLeft = Math.Clamp(desiredLeft, LeftColumn.MinWidth, maxLeft);

            if (available - boundedLeft < minRight)
            {
                boundedLeft = Math.Max(LeftColumn.MinWidth, available - minRight);
            }

            boundedLeft = Math.Clamp(boundedLeft, LeftColumn.MinWidth, maxLeft);
            var boundedRight = Math.Max(minRight, available - boundedLeft);

            LeftColumn.Width = new GridLength(Math.Round(boundedLeft), GridUnitType.Pixel);
            RightColumn.Width = new GridLength(Math.Round(boundedRight), GridUnitType.Pixel);
            _preferredLeftRatio = boundedLeft / available;
            _preferredLeftPanelWidth = (int)Math.Round(boundedLeft);
        }
        finally
        {
            _layoutApplying = false;
        }
    }

    private double CalculateCurrentLeftRatio()
    {
        var splitterWidth = Math.Max(0, SplitterColumn.ActualWidth > 0 ? SplitterColumn.ActualWidth : 6);
        var total = Math.Max(1, RootGrid.ActualWidth - splitterWidth);
        var left = LeftColumn.ActualWidth > 0
            ? LeftColumn.ActualWidth
            : (LeftColumn.Width.IsAbsolute ? LeftColumn.Width.Value : LeftColumn.MinWidth);
        return left / total;
    }

    private void UpdatePreferredLeftLayoutFromCurrent()
    {
        if (!IsLoaded || RootGrid == null)
        {
            return;
        }

        _preferredLeftRatio = NormalizeLeftRatio(CalculateCurrentLeftRatio(), _preferredLeftRatio);
        _preferredLeftPanelWidth = LeftColumn.ActualWidth > 0
            ? (int)Math.Round(LeftColumn.ActualWidth)
            : (int)Math.Round(LeftColumn.Width.IsAbsolute ? LeftColumn.Width.Value : 0);
    }

    private static double NormalizeLeftRatio(double ratio, double fallback)
    {
        if (double.IsNaN(ratio) || double.IsInfinity(ratio) || ratio <= 0)
        {
            return fallback;
        }
        return Math.Clamp(ratio, MinLeftRatio, MaxLeftRatio);
    }

    private static double NormalizeThumbnailSize(double size, double fallback)
    {
        if (double.IsNaN(size) || double.IsInfinity(size) || size <= 0)
        {
            return fallback;
        }
        return Math.Clamp(size, MinThumbnailSize, MaxThumbnailSize);
    }

    private void EnterInitialMaximizedState()
    {
        if (!IsLoaded)
        {
            return;
        }

        WindowState = WindowState.Maximized;
    }

    private void TrackRestoredWindowSize()
    {
        if (WindowState != WindowState.Normal)
        {
            return;
        }

        var width = ActualWidth > 0 ? ActualWidth : Width;
        var height = ActualHeight > 0 ? ActualHeight : Height;
        if (width > 0)
        {
            _restoredWindowWidth = width;
        }
        if (height > 0)
        {
            _restoredWindowHeight = height;
        }
    }

    private void OnRestoreWindowClick(object sender, RoutedEventArgs e)
    {
        if (WindowState == WindowState.Maximized)
        {
            RestoreWindowToManagedBounds();
            return;
        }

        WindowState = WindowState.Maximized;
    }

    private void OnCloseWindowClick(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnTitleBarMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            if (WindowState == WindowState.Maximized)
            {
                RestoreWindowToManagedBounds();
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
            e.Handled = true;
            return;
        }

        if (WindowState != WindowState.Normal)
        {
            return;
        }

        _ = this.SafeDragMove();
    }

    private void RestoreWindowToManagedBounds()
    {
        if (WindowState == WindowState.Normal)
        {
            return;
        }

        WindowState = WindowState.Normal;
        if (_isClosing || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
        {
            return;
        }
        void ApplyRestoredBounds()
        {
            if (WindowState != WindowState.Normal)
            {
                return;
            }

            var plan = ImageManagerRestoreBoundsPolicy.Resolve(
                restoredWidth: _restoredWindowWidth,
                restoredHeight: _restoredWindowHeight,
                defaultWidth: DefaultWindowWidth,
                defaultHeight: DefaultWindowHeight,
                minWidth: MinWidth,
                minHeight: MinHeight,
                workArea: SystemParameters.WorkArea);

            Width = plan.Width;
            Height = plan.Height;
            if (!double.IsNaN(plan.Left))
            {
                Left = plan.Left;
            }
            if (!double.IsNaN(plan.Top))
            {
                Top = plan.Top;
            }

            TrackRestoredWindowSize();
            ApplyAdaptiveLayout();
        }

        var scheduled = false;
        try
        {
            Dispatcher.BeginInvoke(
                new Action(ApplyRestoredBounds),
                DispatcherPriority.Loaded);
            scheduled = true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: restore bounds dispatch failed: {ex.GetType().Name} - {ex.Message}");
        }
        if (!scheduled && Dispatcher.CheckAccess())
        {
            ApplyRestoredBounds();
        }
    }

    private void UpdateWindowStateToggleButton()
    {
        if (WindowStateToggleButton == null)
        {
            return;
        }

        WindowStateToggleButton.Content = WindowState == WindowState.Maximized ? "还原" : "最大化";
    }
}

public sealed class MultiplyConverter : System.Windows.Data.IValueConverter
{
    public double Factor { get; set; } = 1.0;
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is double d ? d * Factor : value;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class FolderVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public sealed class FileVisibilityConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}

public sealed class PdfBackgroundConverter : System.Windows.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => value is bool b && b ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(40, 255, 0, 0)) : System.Windows.Media.Brushes.Transparent;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        => System.Windows.Data.Binding.DoNothing;
}
