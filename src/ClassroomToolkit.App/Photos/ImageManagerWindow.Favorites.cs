using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
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
        NotifyRecentsChanged();
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
            NotifyRecentsChanged();
        }

        NotifyFavoritesChanged();
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
        NotifyFavoritesChanged();

        if (keepInRecents)
        {
            UpdateRecents(path);
        }
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

        NotifyRecentsChanged();
    }

    private void NotifyFavoritesChanged()
    {
        SafeActionExecutionExecutor.TryExecute(
            () => FavoritesChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Favorites)),
            ex => Debug.WriteLine($"ImageManager: favorites callback failed: {ex.Message}"));
    }

    private void NotifyRecentsChanged()
    {
        SafeActionExecutionExecutor.TryExecute(
            () => RecentsChanged?.Invoke(CreateFolderPathSnapshot(ViewModel.Recents)),
            ex => Debug.WriteLine($"ImageManager: recents callback failed: {ex.Message}"));
    }
}
