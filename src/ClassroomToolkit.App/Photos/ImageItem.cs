using System;
using System.ComponentModel;
using System.Windows.Media;

namespace ClassroomToolkit.App.Photos;

public sealed class ImageItem : INotifyPropertyChanged
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

    public bool IsPdf => System.IO.Path.GetExtension(Path).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public string Name => System.IO.Path.GetFileName(Path);

    public string PageBadge => IsPdf && PageCount > 0 ? $"{PageCount}P" : string.Empty;

    public System.Windows.Visibility PageBadgeVisibility => IsPdf && PageCount > 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;

    public string TypeLabel => IsFolder ? "文件夹" : (IsPdf ? "PDF文档" : "图片");

    public string PageLabel => IsPdf ? $"{PageCount} 页" : "-";

    public string ModifiedLabel => Modified == DateTime.MinValue ? "-" : Modified.ToString("yyyy/MM/dd HH:mm");

    private void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
