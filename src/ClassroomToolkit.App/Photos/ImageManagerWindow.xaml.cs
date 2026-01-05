using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow : Window
{
    private const int RecentLimit = 10;
    private readonly ObservableCollection<FolderItem> _favorites = new();
    private readonly ObservableCollection<FolderItem> _recents = new();
    private readonly ObservableCollection<ImageItem> _images = new();
    private string _currentFolder = string.Empty;
    private int _currentIndex = -1;
    private bool _listMode;

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
        _currentIndex = _images.IndexOf(item);
        ImageSelected?.Invoke(_images.Select(image => image.Path).ToList(), _currentIndex);
        ImageList.SelectedItem = null;
        ImageListView.SelectedItem = null;
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

    private void SetViewMode(bool listMode)
    {
        _listMode = listMode;
        ThumbnailViewButton.IsChecked = !listMode;
        ListViewButton.IsChecked = listMode;
        ThumbnailScroll.Visibility = listMode ? Visibility.Collapsed : Visibility.Visible;
        ImageListView.Visibility = listMode ? Visibility.Visible : Visibility.Collapsed;
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

    private void OpenFolder(string path, bool addToRecents = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            ShowEmptyState();
            return;
        }
        _currentFolder = path;
        CurrentFolderText.Text = path;
        LoadImages(path);
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
        _images.Clear();
        var files = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
            .Where(IsMediaFile)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var isPdf = IsPdfFile(file);
            var thumbnail = isPdf ? LoadPdfThumbnail(file) : LoadThumbnail(file);
            var pageCount = isPdf ? TryGetPdfPageCount(file) : 0;
            var modified = GetModifiedTime(file);
            _images.Add(new ImageItem(file, thumbnail, isPdf, pageCount, modified));
        }
        _currentIndex = _images.Count > 0 ? 0 : -1;
        EmptyHintText.Visibility = _images.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowEmptyState()
    {
        _currentFolder = string.Empty;
        CurrentFolderText.Text = "此电脑";
        _images.Clear();
        _currentIndex = -1;
        EmptyHintText.Visibility = Visibility.Visible;
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

    private static bool IsMediaFile(string path)
    {
        if (IsPdfFile(path))
        {
            return true;
        }
        var ext = Path.GetExtension(path);
        return ext != null && (ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsPdfFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext != null && ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
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
        item.Items.Add(new TreeViewItem { Header = "加载中..." });
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
        if (placeholder.Header is not string text || !text.Contains("加载中"))
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

    private sealed record FolderItem(string Path)
    {
        public override string ToString()
        {
            return Path;
        }
    }

    private sealed record ImageItem(string Path, ImageSource? Thumbnail, bool IsPdf, int PageCount, DateTime Modified)
    {
        public string Name => System.IO.Path.GetFileName(Path);
        public string TypeLabel => IsPdf ? "PDF" : "图片";
        public string PageLabel => IsPdf && PageCount > 0 ? $"{PageCount}页" : string.Empty;
        public string ModifiedLabel => Modified == DateTime.MinValue
            ? string.Empty
            : Modified.ToString("yyyy/MM/dd HH:mm");
        public Visibility PageBadgeVisibility => IsPdf && PageCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        public string PageBadge => IsPdf && PageCount > 0 ? $"{PageCount}P" : string.Empty;
    }
}
