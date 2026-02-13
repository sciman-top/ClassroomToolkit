using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using ClassroomToolkit.Interop;
using WpfListViewItem = System.Windows.Controls.ListViewItem;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow : Window
{
    private const int RecentLimit = 10;
    private IntPtr _hwnd;
    private readonly ObservableCollection<FolderItem> _favorites = new();
    private readonly ObservableCollection<FolderItem> _recents = new();
    private readonly ObservableCollection<ImageItem> _images = new();
    private readonly List<ImageItem> _navigableCache = new();
    private bool _navigableDirty = true;
    private readonly List<string> _backStack = new();
    private readonly List<string> _forwardStack = new();
    private string _currentFolder = string.Empty;
    private int _currentIndex = -1;
    private bool _listMode;
    private bool _isClosing;
    private CancellationTokenSource? _thumbnailCts;
    private readonly SemaphoreSlim _thumbnailSemaphore = new(2);
    private int _loadImagesRequestId;

    public event Action<IReadOnlyList<string>, int>? ImageSelected;
    public event Action<IReadOnlyList<string>>? FavoritesChanged;
    public event Action<IReadOnlyList<string>>? RecentsChanged;

    public ImageManagerWindow(IReadOnlyList<string> favorites, IReadOnlyList<string> recents)
    {
        InitializeComponent();
        FavoritesList.ItemsSource = _favorites;
        RecentsList.ItemsSource = _recents;
        ImageList.ItemsSource = _images;
        ImageListView.ItemsSource = _images;
        LoadFolderList(_favorites, favorites);
        LoadFolderList(_recents, recents);
        SetViewMode(listMode: false);
        Loaded += (_, _) => InitializeTree();
        Loaded += (_, _) => InitializeDefaultFolder();
        Closing += (_, _) => BeginClose();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            RemoveMinimizeButton();
        };
    }

    private void InitializeTree()
    {
        FolderTree.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady))
        {
            var node = CreateFolderNode(drive.RootDirectory.FullName, drive.Name);
            FolderTree.Items.Add(node);
        }
    }

    private void InitializeDefaultFolder()
    {
        var firstRecent = _recents.FirstOrDefault();
        if (firstRecent != null)
        {
            OpenFolder(firstRecent.Path, addToRecents: false);
            return;
        }
        ShowEmptyState();
    }

    private void LoadFolderList(ObservableCollection<FolderItem> target, IReadOnlyList<string> source)
    {
        target.Clear();
        foreach (var path in source.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            target.Add(new FolderItem(path));
        }
    }

    private void OnAddFavoriteClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "选择要添加到收藏夹的目录",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
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
        _favorites.Remove(selected);
        FavoritesChanged?.Invoke(_favorites.Select(item => item.Path).ToList());
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
        if (item == null)
        {
            return;
        }
        // 文件夹不触发选中事件
        if (item.IsFolder)
        {
            return;
        }

        var navigableItems = GetNavigableItems();
        _currentIndex = navigableItems.IndexOf(item);
        if (_currentIndex < 0)
        {
            _currentIndex = navigableItems.FindIndex(image =>
                string.Equals(image.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        }
        if (_currentIndex >= 0)
        {
            ImageSelected?.Invoke(navigableItems.Select(image => image.Path).ToList(), _currentIndex);
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
        // 从 OriginalSource 获取实际被点击的元素
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not WpfListViewItem)
        {
            dep = VisualTreeHelper.GetParent(dep);
        }
        if (dep is not WpfListViewItem lvi || lvi.DataContext is not ImageItem item)
        {
            return;
        }
        // 双击文件夹进入
        if (item.IsFolder && Directory.Exists(item.Path))
        {
            OpenFolder(item.Path);
            e.Handled = true;
        }
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
        if (_backStack.Count == 0)
        {
            return;
        }
        // Save current folder to forward stack
        if (!string.IsNullOrEmpty(_currentFolder))
        {
            _forwardStack.Add(_currentFolder);
        }
        var previousFolder = _backStack[^1];
        _backStack.RemoveAt(_backStack.Count - 1);
        OpenFolder(previousFolder, addToRecents: false, navigate: false);
    }

    private void OnForwardClick(object sender, RoutedEventArgs e)
    {
        if (_forwardStack.Count == 0)
        {
            return;
        }
        var nextFolder = _forwardStack[^1];
        _forwardStack.RemoveAt(_forwardStack.Count - 1);
        // Save current folder to back stack
        if (!string.IsNullOrEmpty(_currentFolder))
        {
            _backStack.Add(_currentFolder);
        }
        OpenFolder(nextFolder, addToRecents: true, navigate: false);
    }

    private void OnUpClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentFolder))
        {
            return;
        }
        try
        {
            var parentDir = Directory.GetParent(_currentFolder);
            if (parentDir != null && parentDir.Exists)
            {
                OpenFolder(parentDir.FullName, addToRecents: true);
            }
        }
        catch
        {
            // Ignore errors (e.g., access denied, root directory)
        }
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = _backStack.Count > 0;
        ForwardButton.IsEnabled = _forwardStack.Count > 0;
    }

    private void SetViewMode(bool listMode)
    {
        _listMode = listMode;
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
        if (_listMode)
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
        if (_favorites.Any(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }
        _favorites.Insert(0, new FolderItem(path));
        FavoritesChanged?.Invoke(_favorites.Select(item => item.Path).ToList());
    }

    private void OpenFolder(string path, bool addToRecents = true, bool navigate = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            ShowEmptyState();
            return;
        }
        // Add current folder to back stack when navigating to a new folder
        if (navigate && !string.IsNullOrEmpty(_currentFolder) && !string.Equals(_currentFolder, path, StringComparison.OrdinalIgnoreCase))
        {
            _backStack.Add(_currentFolder);
            _forwardStack.Clear(); // Clear forward stack when navigating to a new location
        }
        _currentFolder = path;
        CurrentFolderText.Text = path;
        StartLoadImages(path);
        UpdateNavigationButtons();
        if (addToRecents)
        {
            UpdateRecents(path);
        }
    }

    private void UpdateRecents(string path)
    {
        var existing = _recents.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            _recents.Remove(existing);
        }
        _recents.Insert(0, new FolderItem(path));
        while (_recents.Count > RecentLimit)
        {
            _recents.RemoveAt(_recents.Count - 1);
        }
        RecentsChanged?.Invoke(_recents.Select(item => item.Path).ToList());
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
        _images.Clear();
        _navigableDirty = true;
        
        // Show loading state if needed, or just clear
        EmptyHintText.Visibility = Visibility.Collapsed;

        try
        {
            // Offload directory enumeration to thread pool
            var result = await Task.Run(() => 
            {
                var list = new List<ImageItem>();
                try 
                {
                    // 1. Folders
                    var dirs = Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly)
                        .Where(d => !IsHiddenFile(d) && !Path.GetFileName(d).StartsWith("."))
                        .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
                        
                    foreach (var dir in dirs)
                    {
                        if (token.IsCancellationRequested) return null;
                        var modified = GetModifiedTime(dir);
                        list.Add(new ImageItem(dir, null, isFolder: true, pageCount: 0, modified: modified, isImage: false));
                    }

                    // 2. PDFs
                    var pdfs = Directory.EnumerateFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly)
                        .Where(f => !IsHiddenFile(f))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

                    foreach (var file in pdfs)
                    {
                        if (token.IsCancellationRequested) return null;
                        var pageCount = TryGetPdfPageCount(file);
                        var modified = GetModifiedTime(file);
                        list.Add(new ImageItem(file, null, isFolder: false, pageCount: pageCount, modified: modified, isImage: false));
                    }

                    // 3. Images
                    var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
                    var imgs = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => !IsHiddenFile(f) && imageExtensions.Contains(Path.GetExtension(f)?.ToLowerInvariant()))
                        .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

                    foreach (var file in imgs)
                    {
                        if (token.IsCancellationRequested) return null;
                        var modified = GetModifiedTime(file);
                        list.Add(new ImageItem(file, null, isFolder: false, pageCount: 0, modified: modified, isImage: true));
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ImageManager: IO Error: {ex.Message}");
                }
                return list;
            }, token);

            if (result == null || token.IsCancellationRequested || !IsCurrentLoadRequest(requestId))
            {
                return;
            }

            // Batch add to collection to minimize UI updates
            foreach (var item in result)
            {
                _images.Add(item);
                // Start thumbnail generation after adding to UI
                if (!item.IsFolder)
                {
                    QueueThumbnailLoad(item, item.IsPdf, token, requestId);
                }
            }

            _currentIndex = GetNavigableItems().Count > 0 ? 0 : -1;
            EmptyHintText.Visibility = _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            // Ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ImageManager: LoadImages Error: {ex}");
            if (IsCurrentLoadRequest(requestId))
            {
                ShowEmptyState();
            }
        }
    }

    private bool IsCurrentLoadRequest(int requestId)
    {
        return requestId == Volatile.Read(ref _loadImagesRequestId);
    }

    private void ShowEmptyState()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
        _currentFolder = string.Empty;
        CurrentFolderText.Text = "此电脑";
        _images.Clear();
        _navigableDirty = true;
        _currentIndex = -1;
        EmptyHintText.Visibility = Visibility.Visible;
        UpdateNavigationButtons();
    }

    private void QueueThumbnailLoad(ImageItem item, bool isPdf, CancellationToken token, int requestId)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _thumbnailSemaphore.WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (token.IsCancellationRequested || _isClosing)
            {
                _thumbnailSemaphore.Release();
                return;
            }
            ImageSource? thumbnail = null;
            try
            {
                thumbnail = isPdf ? LoadPdfThumbnail(item.Path) : LoadThumbnail(item.Path);
            }
            finally
            {
                _thumbnailSemaphore.Release();
            }
            if (thumbnail == null || token.IsCancellationRequested || !IsCurrentLoadRequest(requestId))
            {
                return;
            }
            if (_isClosing || Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)
            {
                return;
            }
            try
            {
                await Dispatcher.BeginInvoke(() =>
                {
                    if (_isClosing || token.IsCancellationRequested || !IsCurrentLoadRequest(requestId))
                    {
                        return;
                    }
                    item.Thumbnail = thumbnail;
                });
            }
            catch
            {
                // Ignore dispatcher shutdown race.
            }
        }, token);
    }

    private void BeginClose()
    {
        if (_isClosing)
        {
            return;
        }
        _isClosing = true;
        _thumbnailCts?.Cancel();
    }

    public bool TryNavigate(int direction)
    {
        var navigableItems = GetNavigableItems();
        if (navigableItems.Count == 0 || _currentIndex < 0)
        {
            return false;
        }
        var next = _currentIndex + direction;
        if (next < 0 || next >= navigableItems.Count)
        {
            return false;
        }
        _currentIndex = next;
        ImageSelected?.Invoke(navigableItems.Select(image => image.Path).ToList(), _currentIndex);
        return true;
    }

    private List<ImageItem> GetNavigableItems()
    {
        if (_navigableDirty)
        {
            _navigableCache.Clear();
            _navigableCache.AddRange(_images.Where(item => !item.IsFolder && (item.IsPdf || item.IsImage)));
            _navigableDirty = false;
        }
        return _navigableCache;
    }

    private static bool IsImageFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext != null && (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPdfFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext != null && ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHiddenFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        }
        catch
        {
            return false;
        }
    }

    private static ImageSource? LoadThumbnail(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 240;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? LoadPdfThumbnail(string path)
    {
        try
        {
            using var doc = PdfDocumentHost.Open(path);
            return doc.RenderPage(1, 96);
        }
        catch
        {
            return null;
        }
    }

    private static int TryGetPdfPageCount(string path)
    {
        try
        {
            using var doc = PdfDocumentHost.Open(path);
            return doc.PageCount;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime GetModifiedTime(string path)
    {
        try
        {
            return File.GetLastWriteTime(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private static TreeViewItem CreateFolderNode(string path, string header)
    {
        var item = new TreeViewItem
        {
            Header = header,
            Tag = path
        };
        item.Items.Add(new TreeViewItem { Header = "\u52a0\u8f7d\u4e2d..." });
        item.Expanded += OnFolderExpanded;
        return item;
    }

    private static void OnFolderExpanded(object sender, RoutedEventArgs e)
    {
        if (sender is not TreeViewItem item)
        {
            return;
        }
        if (item.Items.Count != 1 || item.Items[0] is not TreeViewItem placeholder)
        {
            return;
        }
        if (placeholder.Header is not string text || !text.Contains("\u52a0\u8f7d\u4e2d"))
        {
            return;
        }
        item.Items.Clear();
        if (item.Tag is not string path || string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        try
        {
            foreach (var dir in Directory.GetDirectories(path))
            {
                var name = Path.GetFileName(dir);
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }
                item.Items.Add(CreateFolderNode(dir, name));
            }
        }
        catch
        {
            // Ignore enumeration errors.
        }
    }

    private void RemoveMinimizeButton()
    {
        var hwnd = _hwnd != IntPtr.Zero ? _hwnd : new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }
        var style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GwlStyle);
        if ((style & NativeMethods.WsMinimizeBox) == 0)
        {
            return;
        }
        style &= ~NativeMethods.WsMinimizeBox;
        NativeMethods.SetWindowLong(hwnd, NativeMethods.GwlStyle, style);
        NativeMethods.SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoZOrder | NativeMethods.SwpNoOwnerZOrder | NativeMethods.SwpFrameChanged);
    }

    public void SyncTopmost(bool enabled)
    {
        Topmost = enabled;
        var hwnd = _hwnd != IntPtr.Zero ? _hwnd : new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }
        var insertAfter = enabled ? NativeMethods.HwndTopmost : NativeMethods.HwndNoTopmost;
        NativeMethods.SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow);
    }

    private sealed record FolderItem(string Path)
    {
        public override string ToString()
        {
            return Path;
        }
    }

    private sealed class ImageItem : INotifyPropertyChanged
    {
        private ImageSource? _thumbnail;

        public ImageItem(string path, ImageSource? thumbnail, bool isFolder, int pageCount, DateTime modified, bool isImage)
        {
            Path = path;
            _thumbnail = thumbnail;
            IsFolder = isFolder;
            PageCount = pageCount;
            Modified = modified;
            IsImage = isImage;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string Path { get; }

        public ImageSource? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (ReferenceEquals(_thumbnail, value))
                {
                    return;
                }
                _thumbnail = value;
                OnPropertyChanged(nameof(Thumbnail));
            }
        }

        public bool IsFolder { get; }

        public int PageCount { get; }

        public DateTime Modified { get; }

        public bool IsImage { get; }

        public string Name => System.IO.Path.GetFileName(Path);
        public string TypeLabel => IsFolder ? "文件夹" : IsPdf ? "PDF" : IsImage ? "图片" : string.Empty;
        public string PageLabel => IsPdf && PageCount > 0 ? $"{PageCount}页" : string.Empty;
        public string ModifiedLabel => Modified == DateTime.MinValue
            ? string.Empty
            : Modified.ToString("yyyy/MM/dd HH:mm");
        public Visibility PageBadgeVisibility => IsPdf && PageCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        public string PageBadge => IsPdf && PageCount > 0 ? $"{PageCount}P" : string.Empty;
        public bool IsPdf => !IsFolder && System.IO.Path.GetExtension(Path)?.Equals(".pdf", StringComparison.OrdinalIgnoreCase) == true;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }



}

public sealed class MultiplyConverter : IValueConverter
{
    public double Factor { get; set; } = 1.0;

    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double d)
        {
            return d * Factor;
        }
        return value;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is double d && Factor != 0)
        {
            return d / Factor;
        }
        return value;
    }
}

public sealed class FolderVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is true ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

public sealed class FileVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is true ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}

public sealed class PdfBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is true ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(240, 240, 240)) : System.Windows.Media.Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
