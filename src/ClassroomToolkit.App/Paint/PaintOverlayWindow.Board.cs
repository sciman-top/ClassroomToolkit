using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.Services.Presentation;
using MediaColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    /// <summary>透明但可点击测试的颜色（Alpha=1）</summary>
    private static readonly MediaColor TransparentHitTestColor = MediaColor.FromArgb(1, 255, 255, 255);

    private MediaColor _boardColor = Colors.Transparent;
    private byte _boardOpacity;

    public void SetBoardColor(MediaColor color)
    {
        var wasActive = IsBoardActive();
        _boardColor = color;
        UpdateBoardBackground();
        var isActive = IsBoardActive();
        HandleBoardStateChange(wasActive, isActive);
    }

    public void SetBoardOpacity(byte opacity)
    {
        var wasActive = IsBoardActive();
        _boardOpacity = opacity;
        UpdateBoardBackground();
        UpdateInputPassthrough();
        UpdateWpsNavHookState();
        UpdateFocusAcceptance();
        var isActive = IsBoardActive();
        HandleBoardStateChange(wasActive, isActive);
    }

    private void HandleBoardStateChange(bool wasActive, bool isActive)
    {
        if (isActive && !wasActive)
        {
            if (!_photoModeActive)
            {
                WindowState = WindowState.Normal;
                ApplyFullscreenBounds();
                Dispatcher.BeginInvoke(ApplyFullscreenBounds, DispatcherPriority.Background);
            }
            SaveCurrentPageOnNavigate(forceBackground: false);
            _presentationFullscreenActive = false;
            _currentPresentationType = ClassroomToolkit.Interop.Presentation.PresentationType.None;
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            _boardSuspendedPhotoCache = _photoModeActive;
            if (_photoModeActive && _crossPageDisplayEnabled)
            {
                ClearNeighborPages();
            }
            ClearInkSurfaceState();
            return;
        }
        if (!isActive && wasActive)
        {
            if (!_photoModeActive)
            {
                // Keep overlay on monitor bounds instead of work-area maximize semantics.
                WindowState = WindowState.Normal;
                ApplyFullscreenBounds();
                Dispatcher.BeginInvoke(ApplyFullscreenBounds, DispatcherPriority.Background);
            }
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            ClearInkSurfaceState();
            if (_photoModeActive && _boardSuspendedPhotoCache)
            {
                _boardSuspendedPhotoCache = false;
                _currentCacheScope = InkCacheScope.Photo;
                _currentCacheKey = BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, _photoDocumentIsPdf);
                LoadCurrentPageIfExists();
            }
            if (_photoModeActive && _crossPageDisplayEnabled)
            {
                RequestCrossPageDisplayUpdate("board-exit");
            }
            _refreshOrchestrator.RequestRefresh("board-exit");
        }
    }

    private void UpdateBoardBackground()
    {
        var color = _boardColor;
        var opacity = _boardOpacity;
        if (opacity == 0 || color.A == 0)
        {
            color = TransparentHitTestColor;
        }
        else
        {
            color = MediaColor.FromArgb(opacity, color.R, color.G, color.B);
        }
        OverlayRoot.Background = new SolidColorBrush(color);
        if (_photoModeActive)
        {
            var active = IsBoardActive();
            PhotoBackground.Visibility = active
                ? Visibility.Collapsed
                : (PhotoBackground.Source != null ? Visibility.Visible : Visibility.Collapsed);
            PhotoControlLayer.Visibility = _photoModeActive
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private bool IsBoardActive()
    {
        return _boardOpacity > 0 && _boardColor.A > 0;
    }
}
