using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Runtime.InteropServices;
using WpfListViewItem = System.Windows.Controls.ListViewItem;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow : Window
{
    private const int GwlStyle = -16;
    private const int WsMinimizeBox = 0x20000;
    private static readonly IntPtr HwndTopmost = new(-1);
    private static readonly IntPtr HwndNoTopmost = new(-2);
    private const int SwpNoSize = 0x0001;
    private const int SwpNoMove = 0x0002;
    private const int SwpNoZOrder = 0x0004;
    private const int SwpNoOwnerZOrder = 0x0200;
    private const int SwpFrameChanged = 0x0020;
    private const int SwpNoActivate = 0x0010;
    private const int SwpShowWindow = 0x0040;
    private const int RecentLimit = 10;
    private IntPtr _hwnd;
    private readonly ObservableCollection<FolderItem> _favorites = new();
    private readonly ObservableCollection<FolderItem> _recents = new();
    private readonly ObservableCollection<ImageItem> _images = new();
    private readonly List<string> _backStack = new();
    private readonly List<string> _forwardStack = new();
    private string _currentFolder = string.Empty;
    private int _currentIndex = -1;
    private bool _listMode;
    private CancellationTokenSource? _thumbnailCts;
    private readonly SemaphoreSlim _thumbnailSemaphore = new(2);

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
        if (sender is not System.Windows.Controls.ListView listView || listView.SelectedItem is not ImageItem item)
        {
            return;
        }
        // 文件夹不触发选中事件
        if (item.IsFolder)
        {
            ImageList.SelectedItem = null;
            ImageListView.SelectedItem = null;
            return;
        }
        _currentIndex = _images.IndexOf(item);
        ImageSelected?.Invoke(_images.Select(image => image.Path).ToList(), _currentIndex);
        ImageList.SelectedItem = null;
        ImageListView.SelectedItem = null;
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
        OpenFolder(nextFolder, addToRecents: false, navigate: false);
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
        LoadImages(path);
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

    private void LoadImages(string folder)
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = new CancellationTokenSource();
        var token = _thumbnailCts.Token;
        _images.Clear();

        // 1. 添加非隐藏文件夹
        var folders = Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(d => !IsHiddenFile(d) && !Path.GetFileName(d).StartsWith("."))
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);
        foreach (var dir in folders)
        {
            var modified = GetModifiedTime(dir);
            _images.Add(new ImageItem(dir, null, isFolder: true, pageCount: 0, modified: modified, isImage: false));
        }

        // 2. 添加 PDF 文件
        var pdfFiles = Directory.EnumerateFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly)
            .Where(f => !IsHiddenFile(f))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        foreach (var file in pdfFiles)
        {
            var pageCount = TryGetPdfPageCount(file);
            var modified = GetModifiedTime(file);
            var item = new ImageItem(file, null, isFolder: false, pageCount: pageCount, modified: modified, isImage: false);
            _images.Add(item);
            QueueThumbnailLoad(item, isPdf: true, token);
        }

        // 3. 添加图片文件
        var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
        var imageFiles = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => !IsHiddenFile(f) && imageExtensions.Contains(Path.GetExtension(f)?.ToLowerInvariant()))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
        foreach (var file in imageFiles)
        {
            var modified = GetModifiedTime(file);
            var item = new ImageItem(file, null, isFolder: false, pageCount: 0, modified: modified, isImage: true);
            _images.Add(item);
            QueueThumbnailLoad(item, isPdf: false, token);
        }

        _currentIndex = _images.Count > 0 ? 0 : -1;
        EmptyHintText.Visibility = _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowEmptyState()
    {
        _thumbnailCts?.Cancel();
        _thumbnailCts?.Dispose();
        _thumbnailCts = null;
        _currentFolder = string.Empty;
        CurrentFolderText.Text = "此电脑";
        _images.Clear();
        _currentIndex = -1;
        EmptyHintText.Visibility = Visibility.Visible;
        UpdateNavigationButtons();
    }

    private void QueueThumbnailLoad(ImageItem item, bool isPdf, CancellationToken token)
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
            ImageSource? thumbnail = null;
            try
            {
                thumbnail = isPdf ? LoadPdfThumbnail(item.Path) : LoadThumbnail(item.Path);
            }
            finally
            {
                _thumbnailSemaphore.Release();
            }
            if (thumbnail == null || token.IsCancellationRequested)
            {
                return;
            }
            Dispatcher.Invoke(() =>
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                item.Thumbnail = thumbnail;
            });
        }, token);
    }

    public bool TryNavigate(int direction)
    {
        if (_images.Count == 0 || _currentIndex < 0)
        {
            return false;
        }
        var next = _currentIndex + direction;
        if (next < 0 || next >= _images.Count)
        {
            return false;
        }
        _currentIndex = next;
        ImageSelected?.Invoke(_images.Select(image => image.Path).ToList(), _currentIndex);
        return true;
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
        var style = GetWindowLong(hwnd, GwlStyle);
        if ((style & WsMinimizeBox) == 0)
        {
            return;
        }
        style &= ~WsMinimizeBox;
        SetWindowLong(hwnd, GwlStyle, style);
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0,
            SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoOwnerZOrder | SwpFrameChanged);
    }

    public void SyncTopmost(bool enabled)
    {
        Topmost = enabled;
        var hwnd = _hwnd != IntPtr.Zero ? _hwnd : new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero)
        {
            return;
        }
        var insertAfter = enabled ? HwndTopmost : HwndNoTopmost;
        SetWindowPos(hwnd, insertAfter, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
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

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hwnd, int index, int value);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);

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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
