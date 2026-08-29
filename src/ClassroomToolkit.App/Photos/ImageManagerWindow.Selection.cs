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

        if (item.IsFolder)
        {
            OpenFolder(item.Path);
        }
        else
        {
            OpenPreviewItem(item);
        }

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

        var confirm = TopmostMessageBox.Show(
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
            TopmostMessageBox.Show(
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

    private void OnEnterSelectionModeClick(object sender, RoutedEventArgs e)
    {
        var sourceList = GetActiveImageList();
        var anchorItem = sourceList.SelectedItem as ImageItem
            ?? ViewModel.Images.FirstOrDefault(item => !item.IsFolder)
            ?? ViewModel.Images.FirstOrDefault();
        if (anchorItem == null)
        {
            return;
        }

        EnterMultiSelectMode(anchorItem, sourceList);
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
            () => ImageSelected?.Invoke(GetNavigablePaths(), ViewModel.CurrentIndex),
            ex => Debug.WriteLine($"ImageManager: image selected callback failed: {ex.Message}"));
    }

    private void EnterMultiSelectMode(ImageItem anchorItem, System.Windows.Controls.ListView? sourceList)
    {
        _isMultiSelectMode = true;
        EnterSelectionModeButton.Visibility = Visibility.Collapsed;
        DeleteFilesButton.Visibility = Visibility.Visible;
        SelectAllFilesButton.Visibility = Visibility.Visible;
        ExitSelectionModeButton.Visibility = Visibility.Visible;
        ImageList.SelectionMode = System.Windows.Controls.SelectionMode.Multiple;
        ImageListView.SelectionMode = System.Windows.Controls.SelectionMode.Multiple;

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
        EnterSelectionModeButton.Visibility = Visibility.Visible;
        DeleteFilesButton.Visibility = Visibility.Collapsed;
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
}
