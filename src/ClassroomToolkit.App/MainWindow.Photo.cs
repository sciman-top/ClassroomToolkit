using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ClassroomToolkit.App.Helpers;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Windowing;
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
            _imageManagerWindow = _imageManagerWindowFactory.Create(_settings.PhotoFavoriteFolders, _settings.PhotoRecentFolders);
            _imageManagerWindow.ApplyLayoutSettings(_settings);
            _imageManagerWindow.ViewModel.ShowInkOverlay = _settings.PhotoShowInkOverlay;
            _imageManagerWindow.ImageSelected += OnImageSelected;
            _imageManagerWindow.FavoritesChanged += OnPhotoFavoritesChanged;
            _imageManagerWindow.RecentsChanged += OnPhotoRecentsChanged;
            _imageManagerWindow.ShowInkOverlayChanged += OnImageManagerShowInkOverlayChanged;
            _imageManagerWindow.StateChanged += OnImageManagerStateChanged;
            _imageManagerWindow.Activated += (_, _) => TouchSurface(ZOrderSurface.ImageManager);
            _imageManagerWindow.Closed += (_, _) =>
            {
                var closedWindow = _imageManagerWindow;
                if (closedWindow != null)
                {
                    closedWindow.CaptureLayoutSettings(_settings);
                    closedWindow.StateChanged -= OnImageManagerStateChanged;
                    closedWindow.ShowInkOverlayChanged -= OnImageManagerShowInkOverlayChanged;
                }
                SaveSettings();
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
        PhotoNavigationDiagnostics.Log("MainWindow.Select", $"count={images.Count}, index={index}");
        if (_overlayWindow == null)
        {
            EnsurePaintWindows();
        }
        if (_overlayWindow == null)
        {
            return;
        }
        var shouldCloseImageManager = _imageManagerWindow != null && _imageManagerWindow.IsVisible;
        // Capture "显示笔迹" state before closing ImageManager (Closed handler nullifies the reference)
        var showInk = _imageManagerWindow?.ViewModel?.ShowInkOverlay ?? _settings.PhotoShowInkOverlay;
        if (_settings.PhotoShowInkOverlay != showInk)
        {
            _settings.PhotoShowInkOverlay = showInk;
            SaveSettings();
        }
        if (shouldCloseImageManager)
        {
            // 全屏展示时关闭管理窗口，避免其继续吃键盘事件。
            PhotoNavigationDiagnostics.Log("MainWindow.Select", "close ImageManager");
            _imageManagerWindow!.Owner = null;
            _imageManagerWindow.SyncTopmost(false);
            _imageManagerWindow.Close();
        }
        ShowPaintOverlayIfNeeded();
        if (_toolbarWindow?.BoardActive == true)
        {
            _toolbarWindow.SetBoardActive(false);
        }
        BeginPresentationForegroundSuppression(TimeSpan.FromMilliseconds(800));
        _photoNavigationSession.Reset(images, index);
        // Pass the photo sequence to overlay for cross-page display
        _overlayWindow.SetPhotoSequence(_photoNavigationSession.Sequence, _photoNavigationSession.CurrentIndex);
        var selectedPath = _photoNavigationSession.GetCurrentPath();
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            _overlayWindow.UpdateInkShowEnabled(showInk);
            PhotoNavigationDiagnostics.Log("MainWindow.Select", $"enter path={selectedPath}");
            _overlayWindow.EnterPhotoMode(selectedPath);
            TouchSurface(ZOrderSurface.PhotoFullscreen);
            FocusOverlayForPhotoNavigation(defer: true);
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

    private void OnImageManagerShowInkOverlayChanged(bool enabled)
    {
        if (_settings.PhotoShowInkOverlay == enabled)
        {
            return;
        }

        _settings.PhotoShowInkOverlay = enabled;
        SaveSettings();
        _overlayWindow?.UpdateInkShowEnabled(enabled);
    }

    private void OnPhotoNavigateRequested(int direction)
    {
        if (_overlayWindow == null)
        {
            return;
        }
        PhotoNavigationDiagnostics.Log(
            "MainWindow.FileNav",
            $"dir={direction}, overlayPath={_overlayWindow.CurrentDocumentPath}, overlayType={_overlayWindow.CurrentPhotoFileType}, sessionIndex={_photoNavigationSession.CurrentIndex}, sessionCount={_photoNavigationSession.Sequence.Count}");
        var decision = _photoNavigationSession.Plan(
            _overlayWindow.CurrentDocumentPath,
            direction,
            _overlayWindow.CurrentPhotoFileType);

        // Keep index aligned with the actual page shown in overlay.
        _photoNavigationSession.SyncResolvedIndex(decision);
        PhotoNavigationDiagnostics.Log(
            "MainWindow.FileNav",
            $"decision navigate={decision.ShouldNavigateFile}, resolved={decision.ResolvedCurrentIndex}, next={decision.NextIndex}, currentType={decision.CurrentFileType}");

        if (!_photoNavigationSession.TryApplyFileNavigation(decision, out var nextPath)
            || string.IsNullOrWhiteSpace(nextPath))
        {
            PhotoNavigationDiagnostics.Log("MainWindow.FileNav", "skip file navigation");
            return;
        }

        // 切换到序列中的下一个文件（由统一策略决策）
        // Update overlay's sequence index for cross-page display
        _overlayWindow.SetPhotoSequence(_photoNavigationSession.Sequence, _photoNavigationSession.CurrentIndex);
        _overlayWindow.UpdateInkShowEnabled(_settings.PhotoShowInkOverlay);
        _overlayWindow.EnterPhotoMode(nextPath);
        PhotoNavigationDiagnostics.Log("MainWindow.FileNav", $"enter nextPath={nextPath}");
        FocusOverlayForPhotoNavigation(defer: true);
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
        _imageManagerWindow?.SetKeyboardNavigationSuppressed(active);
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
            FocusOverlayForPhotoNavigation(defer: false);
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

    private void FocusOverlayForPhotoNavigation(bool defer)
    {
        if (_overlayWindow == null)
        {
            return;
        }

        void FocusNow()
        {
            if (_overlayWindow == null || !_overlayWindow.IsVisible)
            {
                return;
            }

            _overlayWindow.Activate();
            Keyboard.Focus(_overlayWindow);
            PhotoNavigationDiagnostics.Log("MainWindow.Focus", $"defer={defer}, focused=true");
        }

        if (defer)
        {
            Dispatcher.BeginInvoke(FocusNow, DispatcherPriority.Input);
            return;
        }

        FocusNow();
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

    internal bool TryHandleOverlayNavigationKeyFromAuxWindow(Key key)
    {
        if (_overlayWindow == null || !_overlayWindow.IsVisible)
        {
            return false;
        }

        if (_overlayWindow.TryHandlePhotoKey(key))
        {
            return true;
        }

        if (_overlayWindow.IsPhotoModeActive || _overlayWindow.IsWhiteboardActive)
        {
            return false;
        }

        if (!IsPresentationNavigationKey(key))
        {
            return false;
        }

        _overlayWindow.ForwardKeyboardToPresentation(key);
        return true;
    }

    private static bool IsPresentationNavigationKey(Key key)
    {
        return key == Key.Left
            || key == Key.Right
            || key == Key.Up
            || key == Key.Down
            || key == Key.PageUp
            || key == Key.PageDown
            || key == Key.Space
            || key == Key.Enter
            || key == Key.Home
            || key == Key.End;
    }
}



