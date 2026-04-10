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
    private const double DefaultLeftRatio = 2.0 / 7.0;
    private const double MinLeftRatio = 0.05;
    private const double MaxLeftRatio = 0.95;
    private const double NarrowWindowThreshold = 1100.0;
    private const double DefaultThumbnailSize = 120.0;
    private const double MinThumbnailSize = 90.0;
    private const double MaxThumbnailSize = 420.0;
    private const double DefaultWindowWidth = 1100.0;
    private const double DefaultWindowHeight = 700.0;
    private const int ThumbnailRefreshDebounceMilliseconds = 90;
    private const int ImageAppendBatchSize = 48;
    private const int FolderNodeRenderBatchSize = 64;

    private IntPtr _hwnd;
    private readonly List<ImageItem> _navigableCache = new();
    private string[] _navigablePathsCache = [];
    private bool _navigableDirty = true;
    private bool _isClosing;
    private CancellationTokenSource? _thumbnailCts;
    private readonly SemaphoreSlim _thumbnailSemaphore = new(ThumbnailWorkerConcurrency);
    private readonly CancellationTokenSource _lifecycleCancellation = new();
    private int _loadImagesRequestId;
    private bool _suppressKeyboardNavigation;
    private bool _layoutApplying;
    private bool _closeCompleted;
    private readonly DispatcherTimer _thumbnailRefreshDebounceTimer;
    private double _preferredLeftRatio = DefaultLeftRatio;
    private int _preferredLeftPanelWidth;
    private double _restoredWindowWidth = DefaultWindowWidth;
    private double _restoredWindowHeight = DefaultWindowHeight;

    public ImageManagerViewModel ViewModel { get; }

    public event Action<IReadOnlyList<string>, int>? ImageSelected;
    public event Action<IReadOnlyList<string>>? FavoritesChanged;
    public event Action<IReadOnlyList<string>>? RecentsChanged;
    public event Action<double, int>? LeftPanelLayoutChanged;

}
