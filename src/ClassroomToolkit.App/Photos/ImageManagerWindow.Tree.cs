using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
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
                    if (string.IsNullOrWhiteSpace(name) || name.StartsWith('.'))
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

#pragma warning disable CA1068 // Signature order is contract-bound across split partials/tests.
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
                catch (OperationCanceledException) when (token.IsCancellationRequested)
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
#pragma warning restore CA1068

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
}
