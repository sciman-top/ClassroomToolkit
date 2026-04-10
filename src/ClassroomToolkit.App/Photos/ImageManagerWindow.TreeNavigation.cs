using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
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
