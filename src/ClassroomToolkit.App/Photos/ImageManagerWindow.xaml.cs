using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow : Window
{
    public ImageManagerWindow(IReadOnlyList<string> favorites, IReadOnlyList<string> recents)
    {
        ViewModel = new ImageManagerViewModel();
        _thumbnailRefreshDebounceTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(ThumbnailRefreshDebounceMilliseconds),
            DispatcherPriority.Background,
            OnThumbnailRefreshDebounceTick,
            Dispatcher.CurrentDispatcher);
        _thumbnailRefreshDebounceTimer.Stop();
        _multiSelectLongPressTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(MultiSelectLongPressMilliseconds),
            DispatcherPriority.Background,
            OnMultiSelectLongPressTick,
            Dispatcher.CurrentDispatcher);
        _multiSelectLongPressTimer.Stop();
        _thumbnailBackgroundQueueTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(ThumbnailBackgroundQueueIntervalMilliseconds),
            DispatcherPriority.Background,
            OnThumbnailBackgroundQueueTick,
            Dispatcher.CurrentDispatcher);
        _thumbnailBackgroundQueueTimer.Stop();
        InitializeComponent();
        DataContext = ViewModel;

        FavoritesList.ItemsSource = ViewModel.Favorites;
        RecentsList.ItemsSource = ViewModel.Recents;
        ImageList.ItemsSource = ViewModel.Images;
        ImageListView.ItemsSource = ViewModel.Images;

        ViewModel.LoadFolderList(ViewModel.Favorites, favorites);
        ViewModel.LoadFolderList(ViewModel.Recents, recents);

        SetViewMode(listMode: false);
        Loaded += OnWindowLoaded;
        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
        SizeChanged += OnWindowSizeChanged;
        StateChanged += OnWindowStateChanged;
        SourceInitialized += OnWindowSourceInitialized;
    }
}
