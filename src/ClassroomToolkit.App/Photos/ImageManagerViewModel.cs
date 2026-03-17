using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using ClassroomToolkit.App.ViewModels;

namespace ClassroomToolkit.App.Photos;

public sealed class ImageManagerViewModel : ViewModelBase
{
    private string _currentFolder = string.Empty;
    private int _currentIndex = -1;
    private bool _listMode;
    private bool _isThumbnailSizeSliderEnabled = true;
    private bool _isBackButtonEnabled;
    private bool _isForwardButtonEnabled;
    private bool _showInkOverlay = true;

    public ObservableCollection<FolderItem> Favorites { get; } = new();
    public ObservableCollection<FolderItem> Recents { get; } = new();
    public ObservableCollection<ImageItem> Images { get; } = new();

    public List<string> BackStack { get; } = new();
    public List<string> ForwardStack { get; } = new();

    public string CurrentFolder
    {
        get => _currentFolder;
        set => SetField(ref _currentFolder, value);
    }

    public int CurrentIndex
    {
        get => _currentIndex;
        set => SetField(ref _currentIndex, value);
    }

    public bool ListMode
    {
        get => _listMode;
        set
        {
            if (SetField(ref _listMode, value))
            {
                IsThumbnailSizeSliderEnabled = !value;
            }
        }
    }

    public bool IsThumbnailSizeSliderEnabled
    {
        get => _isThumbnailSizeSliderEnabled;
        set => SetField(ref _isThumbnailSizeSliderEnabled, value);
    }

    public bool IsBackButtonEnabled
    {
        get => _isBackButtonEnabled;
        set => SetField(ref _isBackButtonEnabled, value);
    }

    public bool IsForwardButtonEnabled
    {
        get => _isForwardButtonEnabled;
        set => SetField(ref _isForwardButtonEnabled, value);
    }

    public bool ShowInkOverlay
    {
        get => _showInkOverlay;
        set => SetField(ref _showInkOverlay, value);
    }

    public void UpdateNavigationStates()
    {
        IsBackButtonEnabled = BackStack.Count > 0;
        IsForwardButtonEnabled = ForwardStack.Count > 0;
    }

    public void LoadFolderList(ObservableCollection<FolderItem> target, IReadOnlyList<string> source)
    {
        target.Clear();
        foreach (var path in source)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            target.Add(new FolderItem(path));
        }
    }
}
