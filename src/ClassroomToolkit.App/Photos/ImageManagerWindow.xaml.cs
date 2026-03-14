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

    private IntPtr _hwnd;
    private readonly List<ImageItem> _navigableCache = new();
    private bool _navigableDirty = true;
    private bool _isClosing;
    private CancellationTokenSource? _thumbnailCts;
    private readonly SemaphoreSlim _thumbnailSemaphore = new(2);
    private int _loadImagesRequestId;
    private bool _suppressKeyboardNavigation;
    private bool _layoutApplying;
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
        InitializeComponent();
        DataContext = ViewModel;

        FavoritesList.ItemsSource = ViewModel.Favorites;
        RecentsList.ItemsSource = ViewModel.Recents;
        ImageList.ItemsSource = ViewModel.Images;
        ImageListView.ItemsSource = ViewModel.Images;

        ViewModel.LoadFolderList(ViewModel.Favorites, favorites);
        ViewModel.LoadFolderList(ViewModel.Recents, recents);

        SetViewMode(listMode: false);
        Loaded += (_, _) => InitializeTree();
        Loaded += (_, _) => InitializeDefaultFolder();
        Loaded += (_, _) => EnterInitialMaximizedState();
        Loaded += (_, _) => ApplyAdaptiveLayout();
        Loaded += (_, _) => UpdateWindowStateToggleButton();
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += (_, _) => BeginClose();
        SizeChanged += (_, _) =>
        {
            TrackRestoredWindowSize();
            ApplyAdaptiveLayout();
        };
        StateChanged += (_, _) =>
        {
            ApplyAdaptiveLayout();
            UpdateWindowStateToggleButton();
        };
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            RemoveMinimizeButton();
        };
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

    private async void InitializeTree()
    {
        FolderTree.Items.Clear();
        FolderTree.Items.Add(new TreeViewItem { Header = "加载中..." });
        try
        {
            var drives = await Task.Run(() => DriveInfo.GetDrives());
            var nodes = new List<TreeViewItem>();
            foreach (var drive in drives)
            {
                string path;
                string header;
                try
                {
                    path = drive.RootDirectory.FullName;
                    header = drive.Name;
                }
                catch
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
                FolderTree.Items.Add(new TreeViewItem { Header = "无可用驱动器" });
                return;
            }
            foreach (var node in nodes)
            {
                FolderTree.Items.Add(node);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ImageManager: InitializeTree Error: {ex}");
            FolderTree.Items.Clear();
            FolderTree.Items.Add(new TreeViewItem { Header = "加载失败" });
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
        catch (Exception ex)
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
        ViewModel.Favorites.Remove(selected);
        FavoritesChanged?.Invoke(ViewModel.Favorites.Select(item => item.Path).ToList());
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
            ImageSelected?.Invoke(navigableItems.Select(image => image.Path).ToList(), ViewModel.CurrentIndex);
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
        catch (Exception ex)
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
    }

    private void OnThumbnailSizeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ViewModel is null || ViewModel.ListMode)
        {
            return;
        }
        ImageList?.Items.Refresh();
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
        FavoritesChanged?.Invoke(ViewModel.Favorites.Select(item => item.Path).ToList());
    }

    private void OpenFolder(string path, bool addToRecents = true, bool navigate = true)
    {
        if (!TryResolveExistingFolder(path, out var folder))
        {
            ShowEmptyState();
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
        RecentsChanged?.Invoke(ViewModel.Recents.Select(item => item.Path).ToList());
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

            foreach (var item in result)
            {
                ViewModel.Images.Add(item);
                if (!item.IsFolder)
                {
                    QueueThumbnailLoad(item, item.IsPdf, token, requestId);
                }
            }

            ViewModel.CurrentIndex = GetNavigableItems().Count > 0 ? 0 : -1;
            EmptyHintText.Visibility = ViewModel.Images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
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
        _ = Task.Run(async () =>
        {
            try { await _thumbnailSemaphore.WaitAsync(token); }
            catch (OperationCanceledException) { return; }

            if (token.IsCancellationRequested || _isClosing)
            {
                _thumbnailSemaphore.Release();
                return;
            }

            ImageSource? thumbnail = null;
            try { thumbnail = isPdf ? LoadPdfThumbnail(item.Path) : LoadThumbnail(item.Path); }
            finally { _thumbnailSemaphore.Release(); }

            if (thumbnail == null || token.IsCancellationRequested || requestId != Volatile.Read(ref _loadImagesRequestId))
            {
                return;
            }

            await TryDispatchThumbnailUpdateAsync(item, thumbnail, token, requestId);
        }, token);
    }

    private async Task TryDispatchThumbnailUpdateAsync(
        ImageItem item,
        ImageSource thumbnail,
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
                }
                return;
            }

            await Dispatcher.InvokeAsync(() =>
            {
                if (!_isClosing && !token.IsCancellationRequested && requestId == Volatile.Read(ref _loadImagesRequestId))
                {
                    item.Thumbnail = thumbnail;
                }
            });
        }
        catch (Exception ex)
        {
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
        UpdatePreferredLeftLayoutFromCurrent();
        LeftPanelLayoutChanged?.Invoke(_preferredLeftRatio, _preferredLeftPanelWidth);
        _thumbnailCts?.Cancel();
    }

    public bool TryNavigate(int direction)
    {
        var navigableItems = GetNavigableItems();
        if (navigableItems.Count == 0 || ViewModel.CurrentIndex < 0) return false;
        var next = ViewModel.CurrentIndex + direction;
        if (next < 0 || next >= navigableItems.Count) return false;
        ViewModel.CurrentIndex = next;
        ImageSelected?.Invoke(navigableItems.Select(image => image.Path).ToList(), ViewModel.CurrentIndex);
        return true;
    }

    private List<ImageItem> GetNavigableItems()
    {
        if (_navigableDirty)
        {
            _navigableCache.Clear();
            _navigableCache.AddRange(ViewModel.Images.Where(item => !item.IsFolder && (item.IsPdf || item.IsImage)));
            _navigableDirty = false;
        }
        return _navigableCache;
    }

    private static TreeViewItem CreateFolderNode(string path, string header)
    {
        var item = new TreeViewItem { Header = header, Tag = path };
        item.Items.Add(new TreeViewItem { Header = "\u52a0\u8f7d\u4e2d..." });
        item.Expanded += OnFolderExpanded;
        return item;
    }

    private static void OnFolderExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item || item.Items.Count != 1 || item.Items[0] is not TreeViewItem placeholder) return;
        if (placeholder.Header is not string text || !text.Contains("\u52a0\u8f7d\u4e2d")) return;
        item.Items.Clear();
        if (item.Tag is not string path || string.IsNullOrWhiteSpace(path)) return;
        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                var name = Path.GetFileName(dir);
                if (!string.IsNullOrWhiteSpace(name)) item.Items.Add(CreateFolderNode(dir, name));
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatFolderExpandFailureMessage(
                    path,
                    ex.Message));
        }
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

    private void OnMainColumnSplitterDragDelta(object sender, DragDeltaEventArgs e)
    {
        UpdatePreferredLeftLayoutFromCurrent();
    }

    private void OnMainColumnSplitterDragCompleted(object sender, DragCompletedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            UpdatePreferredLeftLayoutFromCurrent();
            LeftPanelLayoutChanged?.Invoke(_preferredLeftRatio, _preferredLeftPanelWidth);
        }, DispatcherPriority.Background);
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

        try
        {
            DragMove();
        }
        catch
        {
            // Ignore drag exceptions from transient mouse capture state.
        }
    }

    private void RestoreWindowToManagedBounds()
    {
        if (WindowState == WindowState.Normal)
        {
            return;
        }

        WindowState = WindowState.Normal;
        Dispatcher.BeginInvoke(() =>
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
        }, DispatcherPriority.Loaded);
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
