using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.Interop.Presentation;

namespace ClassroomToolkit.App;

/// <summary>
/// Photo teaching, image manager, photo navigation, and presentation foreground management.
/// </summary>
public partial class MainWindow
{
    private void OnOpenPhotoTeaching()
    {
        if (_imageManagerWindow == null)
        {
            _imageManagerWindow = new ImageManagerWindow(_settings.PhotoFavoriteFolders, _settings.PhotoRecentFolders);
            _imageManagerWindow.ImageSelected += OnImageSelected;
            _imageManagerWindow.FavoritesChanged += OnPhotoFavoritesChanged;
            _imageManagerWindow.RecentsChanged += OnPhotoRecentsChanged;
            _imageManagerWindow.StateChanged += OnImageManagerStateChanged;
            _imageManagerWindow.Activated += (_, _) => TouchSurface(ZOrderSurface.ImageManager);
            _imageManagerWindow.Closed += (_, _) =>
            {
                _imageManagerWindow.StateChanged -= OnImageManagerStateChanged;
                _imageManagerWindow = null;
                ApplyZOrderPolicy();
            };
        }
        _imageManagerWindow.Owner = _overlayWindow != null && _overlayWindow.IsVisible
            ? _overlayWindow
            : null;
        _imageManagerWindow.Show();
        if (_imageManagerWindow.WindowState == WindowState.Minimized)
        {
            _imageManagerWindow.WindowState = WindowState.Normal;
        }
        _imageManagerWindow.SyncTopmost(true);
        TouchSurface(ZOrderSurface.ImageManager);
    }

    private void OnImageManagerStateChanged(object? sender, EventArgs e)
    {
        if (_imageManagerWindow == null
            || _imageManagerWindow.WindowState != WindowState.Minimized)
        {
            return;
        }
        if (_overlayWindow == null || !_overlayWindow.IsVisible)
        {
            return;
        }
        if (_overlayWindow.WindowState != WindowState.Minimized)
        {
            return;
        }
        Dispatcher.BeginInvoke(() =>
        {
            if (_overlayWindow != null && _overlayWindow.WindowState == WindowState.Minimized)
            {
                _overlayWindow.WindowState = WindowState.Normal;
            }
        }, DispatcherPriority.Background);
        ApplyZOrderPolicy();
    }

    private void OnImageSelected(IReadOnlyList<string> images, int index)
    {
        if (_overlayWindow == null)
        {
            EnsurePaintWindows();
        }
        if (_overlayWindow == null)
        {
            return;
        }
        ShowPaintOverlayIfNeeded();
        if (_toolbarWindow?.BoardActive == true)
        {
            _toolbarWindow.SetBoardActive(false);
        }
        BeginPresentationForegroundSuppression(TimeSpan.FromMilliseconds(800));
        _photoSequence = images.ToList();
        _photoSequenceIndex = index;
        // Pass the photo sequence to overlay for cross-page display
        _overlayWindow.SetPhotoSequence(_photoSequence, _photoSequenceIndex);
        if (_photoSequenceIndex >= 0 && _photoSequenceIndex < _photoSequence.Count)
        {
            _overlayWindow.EnterPhotoMode(_photoSequence[_photoSequenceIndex]);
            TouchSurface(ZOrderSurface.PhotoFullscreen);
        }
    }

    private void OnPhotoFavoritesChanged(IReadOnlyList<string> favorites)
    {
        _settings.PhotoFavoriteFolders = favorites.ToList();
        SaveSettings();
    }

    private void OnPhotoRecentsChanged(IReadOnlyList<string> recents)
    {
        _settings.PhotoRecentFolders = recents.ToList();
        SaveSettings();
    }

    private void OnPhotoNavigateRequested(int direction)
    {
        if (_overlayWindow == null)
        {
            return;
        }
        // 优先尝试通过 ImageManager 导航(如果窗口打开)
        if (_imageManagerWindow != null && _imageManagerWindow.TryNavigate(direction))
        {
            return; // 成功导航到下一个文件
        }
        // 如果没有照片序列,直接返回(不做任何事,保持当前状态)
        if (_photoSequence.Count == 0 || _photoSequenceIndex < 0)
        {
            return;
        }
        // 计算下一个索引
        var next = _photoSequenceIndex + direction;
        // 到达边界检查:如果超出范围,直接返回而不退出全屏
        // 这样可以保持当前文件的显示,不会意外关闭全屏模式
        if (next < 0 || next >= _photoSequence.Count)
        {
            return; // 已到达第一个/最后一个文件,保持当前状态
        }
        // 切换到序列中的下一个文件
        _photoSequenceIndex = next;
        // Update overlay's sequence index for cross-page display
        _overlayWindow.SetPhotoSequence(_photoSequence, _photoSequenceIndex);
        _overlayWindow.EnterPhotoMode(_photoSequence[_photoSequenceIndex]);
    }

    private void BeginPresentationForegroundSuppression(TimeSpan duration)
    {
        if (_presentationForegroundSuppression == null)
        {
            _presentationForegroundSuppression = PresentationWindowFocus.SuppressForeground();
        }
        _presentationForegroundSuppressionTimer.Stop();
        _presentationForegroundSuppressionTimer.Interval = duration;
        _presentationForegroundSuppressionTimer.Start();
    }

    private void ReleasePresentationForegroundSuppression()
    {
        _presentationForegroundSuppressionTimer.Stop();
        _presentationForegroundSuppression?.Dispose();
        _presentationForegroundSuppression = null;
    }

    private void OnPhotoModeChanged(bool active)
    {
        if (_overlayWindow == null || _toolbarWindow == null)
        {
            return;
        }
        if (active)
        {
            if (_toolbarWindow.WindowState == WindowState.Minimized)
            {
                _toolbarWindow.WindowState = WindowState.Normal;
            }
            _toolbarWindow.Show();
            _toolbarWindow.SyncTopmost(true);
            if (_rollCallWindow != null)
            {
                if (_rollCallWindow.IsVisible)
                {
                    _rollCallWindow.SyncTopmost(true);
                }
            }
            TouchSurface(ZOrderSurface.PhotoFullscreen, applyPolicy: false);
            ApplyZOrderPolicy();
            return;
        }
        if (_overlayWindow.IsVisible && _toolbarWindow.Owner != _overlayWindow)
        {
            _toolbarWindow.Owner = _overlayWindow;
        }
        _toolbarWindow.SyncTopmost(true);
        if (_rollCallWindow != null && _rollCallWindow.IsVisible && _overlayWindow.IsVisible)
        {
            _rollCallWindow.Owner = _overlayWindow;
            _rollCallWindow.SyncTopmost(true);
        }
        ApplyZOrderPolicy();
    }

    private void OnOverlayActivated()
    {
        if (_overlayWindow == null)
        {
            return;
        }
        if (_overlayWindow.IsPhotoModeActive)
        {
            TouchSurface(ZOrderSurface.PhotoFullscreen, applyPolicy: false);
        }
        else if (_toolbarWindow?.BoardActive == true)
        {
            TouchSurface(ZOrderSurface.Whiteboard, applyPolicy: false);
        }
        ApplyZOrderPolicy();
    }

    private void OnPresentationFullscreenDetected()
    {
        if (_overlayWindow == null)
        {
            return;
        }
        ApplyZOrderPolicy();
    }

    private void OnPresentationForegroundDetected(PresentationType type)
    {
        if (_overlayWindow == null)
        {
            return;
        }
        TouchSurface(ZOrderSurface.PresentationFullscreen, applyPolicy: false);
        ApplyZOrderPolicy();
    }

    private void OnPhotoForegroundDetected()
    {
        if (_overlayWindow == null)
        {
            return;
        }
        TouchSurface(ZOrderSurface.PhotoFullscreen, applyPolicy: false);
        ApplyZOrderPolicy();
    }

    private void OnPhotoUnifiedTransformChanged(
        double scaleX,
        double scaleY,
        double translateX,
        double translateY)
    {
        var changed = !_settings.PhotoUnifiedTransformEnabled
            || !AreClose(_settings.PhotoUnifiedScaleX, scaleX)
            || !AreClose(_settings.PhotoUnifiedScaleY, scaleY)
            || !AreClose(_settings.PhotoUnifiedTranslateX, translateX)
            || !AreClose(_settings.PhotoUnifiedTranslateY, translateY);

        _settings.PhotoUnifiedTransformEnabled = true;
        _settings.PhotoUnifiedScaleX = scaleX;
        _settings.PhotoUnifiedScaleY = scaleY;
        _settings.PhotoUnifiedTranslateX = translateX;
        _settings.PhotoUnifiedTranslateY = translateY;

        if (changed)
        {
            SaveSettings();
        }
    }

    private static bool AreClose(double left, double right)
    {
        return Math.Abs(left - right) < 0.0001;
    }
}
