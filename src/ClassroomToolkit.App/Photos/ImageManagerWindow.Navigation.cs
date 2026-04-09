using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClassroomToolkit.App.Windowing;
using WpfListViewItem = System.Windows.Controls.ListViewItem;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
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
            Debug.WriteLine(
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
}
