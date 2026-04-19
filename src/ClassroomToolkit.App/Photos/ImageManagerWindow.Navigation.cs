using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
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

        var ownerHandle = _hwnd != IntPtr.Zero ? _hwnd : new WindowInteropHelper(this).Handle;
        var owner = ownerHandle != IntPtr.Zero ? new Win32DialogOwner(ownerHandle) : null;
        var restoreOwnerTopmost = Topmost;
        var loweredOwnerTopmost = false;
        System.Windows.Forms.DialogResult result;

        using var _ = FloatingTopmostDialogSuppressionState.Enter();
        try
        {
            if (restoreOwnerTopmost)
            {
                Topmost = false;
                loweredOwnerTopmost = true;
            }

            result = owner == null
                ? dialog.ShowDialog()
                : dialog.ShowDialog(owner);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatFavoriteFolderDialogFailureMessage(
                    ex.Message));
            return;
        }
        finally
        {
            if (loweredOwnerTopmost)
            {
                SafeActionExecutionExecutor.TryExecute(
                    () =>
                    {
                        Topmost = restoreOwnerTopmost;
                        WindowTopmostExecutor.ApplyNoActivate(this, enabled: restoreOwnerTopmost, enforceZOrder: true);
                    },
                    ex => Debug.WriteLine(
                        ImageManagerDiagnosticsPolicy.FormatFavoriteFolderDialogFailureMessage(
                            $"restore-topmost-failed: {ex.Message}")));
            }
        }
        if (result != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }
        AddFavorite(dialog.SelectedPath);
    }

    private sealed class Win32DialogOwner : System.Windows.Forms.IWin32Window
    {
        internal Win32DialogOwner(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
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
        if (_suppressSelectionChanged)
        {
            return;
        }

        if (!_isMultiSelectMode)
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
            return;
        }

        UpdateSelectionActionState();
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

    private void OnImageListPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (_isMultiSelectMode)
        {
            if (TryResolveImageItemFromPointer(sender, e.OriginalSource, out _, out _))
            {
                // Multi-select mode uses custom tap toggle on PointerUp.
                e.Handled = true;
            }
            return;
        }

        if (!TryResolveImageItemFromPointer(sender, e.OriginalSource, out var sourceList, out var item))
        {
            StopLongPressTracking(resetTriggered: true);
            return;
        }

        _longPressSourceList = sourceList;
        _longPressCandidateItem = item;
        _longPressStartPoint = e.GetPosition(sourceList);
        _longPressTriggered = false;
        _multiSelectLongPressTimer.Stop();
        _multiSelectLongPressTimer.Start();
    }

    private void OnImageListPointerMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_multiSelectLongPressTimer.IsEnabled || _longPressSourceList == null)
        {
            return;
        }

        var current = e.GetPosition(_longPressSourceList);
        if (Math.Abs(current.X - _longPressStartPoint.X) > MultiSelectLongPressMoveTolerance ||
            Math.Abs(current.Y - _longPressStartPoint.Y) > MultiSelectLongPressMoveTolerance)
        {
            StopLongPressTracking(resetTriggered: false);
        }
    }

    private void OnImageListPointerLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        StopLongPressTracking(resetTriggered: false);
    }

    private void OnImageListPointerUp(object sender, MouseButtonEventArgs e)
    {
        _multiSelectLongPressTimer.Stop();
        if (_longPressTriggered)
        {
            StopLongPressTracking(resetTriggered: true);
            e.Handled = true;
            return;
        }

        if (!TryResolveImageItemFromPointer(sender, e.OriginalSource, out var sourceList, out var item))
        {
            StopLongPressTracking(resetTriggered: true);
            return;
        }

        if (_isMultiSelectMode)
        {
            ToggleMultiSelectItem(sourceList, item);
            e.Handled = true;
            StopLongPressTracking(resetTriggered: true);
            return;
        }

        if (!ImageManagerActivationPolicy.ShouldOpenOnSingleClick(item.IsFolder, item.IsPdf, item.IsImage))
        {
            StopLongPressTracking(resetTriggered: true);
            return;
        }

        OpenPreviewItem(item);
        e.Handled = true;
        StopLongPressTracking(resetTriggered: true);
    }

    private void OnImageListMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_isMultiSelectMode)
        {
            return;
        }

        if (!TryResolveImageItemFromPointer(sender, e.OriginalSource, out _, out var item))
        {
            return;
        }

        if (!ImageManagerActivationPolicy.ShouldOpenOnDoubleClick(item.IsFolder, item.IsPdf, item.IsImage))
        {
            return;
        }

        if (item.IsFolder)
        {
            if (!Directory.Exists(item.Path))
            {
                return;
            }

            OpenFolder(item.Path);
            e.Handled = true;
            return;
        }

        OpenPreviewItem(item);
        e.Handled = true;
    }

    private void OnMultiSelectLongPressTick(object? sender, EventArgs e)
    {
        _multiSelectLongPressTimer.Stop();
        if (_longPressCandidateItem == null)
        {
            return;
        }

        _longPressTriggered = true;
        EnterMultiSelectMode(_longPressCandidateItem, _longPressSourceList);
    }

    private void OnDeleteSelectedFilesClick(object sender, RoutedEventArgs e)
    {
        var selectedFiles = GetSelectedFileItems().ToList();
        if (selectedFiles.Count == 0)
        {
            return;
        }

        var confirm = System.Windows.MessageBox.Show(
            this,
            $"确定删除已选中的 {selectedFiles.Count} 个文件吗？",
            "删除文件",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.No);
        if (confirm != System.Windows.MessageBoxResult.Yes)
        {
            return;
        }

        var deletedCount = 0;
        var failedCount = 0;
        foreach (var file in selectedFiles)
        {
            if (TryDeleteImageFile(file.Path))
            {
                deletedCount++;
            }
            else
            {
                failedCount++;
            }
        }

        if (!string.IsNullOrWhiteSpace(ViewModel.CurrentFolder))
        {
            StartLoadImages(ViewModel.CurrentFolder);
        }
        ExitMultiSelectMode();

        if (failedCount > 0)
        {
            System.Windows.MessageBox.Show(
                this,
                $"已删除 {deletedCount} 个文件，另有 {failedCount} 个文件删除失败（可能被占用或无权限）。",
                "删除结果",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }
    }

    private void OnSelectAllFilesClick(object sender, RoutedEventArgs e)
    {
        if (!_isMultiSelectMode)
        {
            return;
        }

        var activeList = GetActiveImageList();
        _suppressSelectionChanged = true;
        try
        {
            activeList.SelectedItems.Clear();
            foreach (var file in ViewModel.Images.Where(image => !image.IsFolder))
            {
                activeList.SelectedItems.Add(file);
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        UpdateSelectionActionState();
    }

    private void OnExitSelectionModeClick(object sender, RoutedEventArgs e)
    {
        ExitMultiSelectMode();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_isMultiSelectMode && e.Key == Key.Escape)
        {
            ExitMultiSelectMode();
            e.Handled = true;
            return;
        }

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
        if (_isMultiSelectMode)
        {
            ExitMultiSelectMode();
        }

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

        if (_isMultiSelectMode)
        {
            ExitMultiSelectMode();
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

    private bool TryResolveImageItemFromPointer(object sender, object originalSource, out System.Windows.Controls.ListView sourceList, out ImageItem item)
    {
        sourceList = sender as System.Windows.Controls.ListView ?? GetActiveImageList();
        item = null!;

        var dep = originalSource as DependencyObject;
        while (dep != null && dep is not WpfListViewItem)
        {
            dep = VisualTreeHelper.GetParent(dep);
        }

        if (dep is not WpfListViewItem listViewItem || listViewItem.DataContext is not ImageItem resolved)
        {
            return false;
        }

        item = resolved;
        return true;
    }

    private System.Windows.Controls.ListView GetActiveImageList()
    {
        return ViewModel.ListMode ? ImageListView : ImageList;
    }

    private void OpenPreviewItem(ImageItem item)
    {
        if (item.IsFolder)
        {
            return;
        }

        var navigableItems = GetNavigableItems();
        var index = navigableItems.IndexOf(item);
        if (index < 0)
        {
            index = navigableItems.FindIndex(image =>
                string.Equals(image.Path, item.Path, StringComparison.OrdinalIgnoreCase));
        }

        if (index < 0)
        {
            return;
        }

        ViewModel.CurrentIndex = index;
        SafeActionExecutionExecutor.TryExecute(
            () => ImageSelected?.Invoke(GetNavigablePaths(), index),
            ex => Debug.WriteLine($"ImageManager: image selected callback failed: {ex.Message}"));
    }

    private void EnterMultiSelectMode(ImageItem anchorItem, System.Windows.Controls.ListView? sourceList)
    {
        _isMultiSelectMode = true;
        ImageList.SelectionMode = System.Windows.Controls.SelectionMode.Multiple;
        ImageListView.SelectionMode = System.Windows.Controls.SelectionMode.Multiple;
        SelectAllFilesButton.Visibility = Visibility.Visible;
        ExitSelectionModeButton.Visibility = Visibility.Visible;

        var selectionList = sourceList ?? GetActiveImageList();
        _suppressSelectionChanged = true;
        try
        {
            ImageList.SelectedItems.Clear();
            ImageListView.SelectedItems.Clear();
            selectionList.SelectedItems.Add(anchorItem);
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        UpdateSelectionActionState();
    }

    private void ExitMultiSelectMode()
    {
        _isMultiSelectMode = false;
        SelectAllFilesButton.Visibility = Visibility.Collapsed;
        ExitSelectionModeButton.Visibility = Visibility.Collapsed;

        _suppressSelectionChanged = true;
        try
        {
            ImageList.SelectedItems.Clear();
            ImageListView.SelectedItems.Clear();
            ImageList.SelectionMode = System.Windows.Controls.SelectionMode.Single;
            ImageListView.SelectionMode = System.Windows.Controls.SelectionMode.Single;
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        UpdateSelectionActionState();
    }

    private void ToggleMultiSelectItem(System.Windows.Controls.ListView sourceList, ImageItem item)
    {
        _suppressSelectionChanged = true;
        try
        {
            if (sourceList.SelectedItems.Contains(item))
            {
                sourceList.SelectedItems.Remove(item);
            }
            else
            {
                sourceList.SelectedItems.Add(item);
            }
        }
        finally
        {
            _suppressSelectionChanged = false;
        }

        UpdateSelectionActionState();
    }

    private IEnumerable<ImageItem> GetSelectedFileItems()
    {
        var selectedItems = GetActiveImageList().SelectedItems.OfType<ImageItem>();
        foreach (var item in selectedItems)
        {
            if (item.IsFolder)
            {
                continue;
            }

            yield return item;
        }
    }

    private void UpdateSelectionActionState()
    {
        var selectedCount = GetSelectedFileItems().Count();
        DeleteFilesButton.Content = selectedCount > 0 ? $"删除({selectedCount})" : "删除";
        DeleteFilesButton.IsEnabled = selectedCount > 0;
    }

    private void StopLongPressTracking(bool resetTriggered)
    {
        _multiSelectLongPressTimer.Stop();
        _longPressCandidateItem = null;
        _longPressSourceList = null;
        if (resetTriggered)
        {
            _longPressTriggered = false;
        }
    }

    private bool TryDeleteImageFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            _inkPersistence?.DeleteInkForFile(path);
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"ImageManager: delete-file failed path={path} ex={ex.GetType().Name} msg={ex.Message}");
            return false;
        }
    }
}
