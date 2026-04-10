using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        EnsureRasterSurface();
        if (!_photoModeActive || _photoUserTransformDirty)
        {
            return;
        }
        if (PhotoBackground.Source is BitmapSource bitmap)
        {
            ApplyPhotoFitToViewport(bitmap);
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        if (WindowState == WindowState.Minimized)
        {
            _photoRestoreFullscreenPending = PhotoWindowStateRestorePolicy.ShouldArmFullscreenRestore(_photoFullscreen);
            SavePhotoTransformState(true);
            return;
        }
        if (PhotoWindowStateRestorePolicy.ShouldRestoreFullscreen(_photoRestoreFullscreenPending, WindowState))
        {
            _photoRestoreFullscreenPending = false;
            _photoFullscreen = true;
            SetPhotoWindowMode(fullscreen: true);

            if (_photoDocumentIsPdf && _pdfDocument != null)
            {
                RenderPdfPage(_currentPageIndex);
            }

            if (_rememberPhotoTransform)
            {
                var key = GetCurrentPhotoTransformKey();
                if (!IsCrossPageDisplayActive() && TryApplyStoredPhotoTransform(key))
                {
                }
                else
                {
                    ApplyLastUnifiedPhotoTransform(markUserDirty: false);
                }
                ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
                RequestPhotoTransformInkRedraw();
            }
        }
    }

    private readonly struct PhotoTransformState
    {
        public PhotoTransformState(double scaleX, double scaleY, double translateX, double translateY, bool userAdjusted)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            TranslateX = translateX;
            TranslateY = translateY;
            UserAdjusted = userAdjusted;
        }

        public double ScaleX { get; }
        public double ScaleY { get; }
        public double TranslateX { get; }
        public double TranslateY { get; }
        public bool UserAdjusted { get; }
    }

    private string GetCurrentPhotoTransformKey()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return string.Empty;
        }
        return BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, _photoDocumentIsPdf);
    }

    private bool TryApplyStoredPhotoTransform(string cacheKey)
    {
        if (!_rememberPhotoTransform)
        {
            _photoUserTransformDirty = false;
            return false;
        }
        if (IsCrossPageDisplayActive())
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _photoUserTransformDirty = false;
            return false;
        }
        if (!_photoPageTransforms.TryGetValue(cacheKey, out var state))
        {
            _photoUserTransformDirty = false;
            return false;
        }
        EnsurePhotoTransformsWritable();
        _photoScale.ScaleX = state.ScaleX;
        _photoScale.ScaleY = state.ScaleY;
        _photoTranslate.X = state.TranslateX;
        _photoTranslate.Y = state.TranslateY;
        _photoUserTransformDirty = state.UserAdjusted;
        return true;
    }

    private void SavePhotoTransformState(bool userAdjusted)
    {
        _lastPhotoScaleX = _photoScale.ScaleX;
        _lastPhotoScaleY = _photoScale.ScaleY;
        _lastPhotoTranslateX = _photoTranslate.X;
        _lastPhotoTranslateY = _photoTranslate.Y;
        _photoUserTransformDirty = userAdjusted;
        if (_rememberPhotoTransform && IsCrossPageDisplayActive())
        {
            _photoUnifiedTransformReady = true;
            SchedulePhotoUnifiedTransformSave();
            return;
        }
        if (_rememberPhotoTransform && _photoModeActive)
        {
            var key = GetCurrentPhotoTransformKey();
            if (!string.IsNullOrWhiteSpace(key))
            {
                _photoPageTransforms[key] = new PhotoTransformState(
                    _photoScale.ScaleX,
                    _photoScale.ScaleY,
                    _photoTranslate.X,
                    _photoTranslate.Y,
                    userAdjusted);
            }
        }
    }

    private void SchedulePhotoTransformSave(bool userAdjusted)
    {
        if (!_photoModeActive)
        {
            return;
        }
        _photoTransformSavePending = true;
        if (userAdjusted)
        {
            _photoTransformSaveUserAdjusted = true;
        }
        if (_photoTransformSaveTimer == null)
        {
            _photoTransformSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PhotoTransformTimingDefaults.TransformSaveDebounceMs)
            };
            _photoTransformSaveTimer.Tick += OnPhotoTransformSaveTimerTick;
        }
        _photoTransformSaveTimer.Stop();
        _photoTransformSaveTimer.Start();
    }

    private void FlushPhotoTransformSave()
    {
        if (!_photoTransformSavePending)
        {
            return;
        }
        _photoTransformSaveTimer?.Stop();
        var adjusted = _photoTransformSaveUserAdjusted;
        _photoTransformSavePending = false;
        _photoTransformSaveUserAdjusted = false;
        SavePhotoTransformState(adjusted);
    }

    private void SchedulePhotoUnifiedTransformSave()
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }
        _pendingUnifiedScaleX = _lastPhotoScaleX;
        _pendingUnifiedScaleY = _lastPhotoScaleY;
        _pendingUnifiedTranslateX = _lastPhotoTranslateX;
        _pendingUnifiedTranslateY = _lastPhotoTranslateY;
        if (_photoUnifiedTransformSaveTimer == null)
        {
            _photoUnifiedTransformSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PhotoTransformTimingDefaults.UnifiedTransformBroadcastDebounceMs)
            };
            _photoUnifiedTransformSaveTimer.Tick += OnPhotoUnifiedTransformSaveTimerTick;
        }
        _photoUnifiedTransformSaveTimer.Stop();
        _photoUnifiedTransformSaveTimer.Start();
    }

    private void OnPhotoTransformSaveTimerTick(object? sender, EventArgs e)
    {
        _photoTransformSaveTimer?.Stop();
        if (!_photoTransformSavePending)
        {
            _photoTransformSaveUserAdjusted = false;
            return;
        }
        var adjusted = _photoTransformSaveUserAdjusted;
        _photoTransformSavePending = false;
        _photoTransformSaveUserAdjusted = false;
        SavePhotoTransformState(adjusted);
    }

    private void OnPhotoUnifiedTransformSaveTimerTick(object? sender, EventArgs e)
    {
        _photoUnifiedTransformSaveTimer?.Stop();
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoUnifiedTransformChanged?.Invoke(
                _pendingUnifiedScaleX,
                _pendingUnifiedScaleY,
                _pendingUnifiedTranslateX,
                _pendingUnifiedTranslateY),
            ex => Debug.WriteLine($"[PhotoUnifiedTransformChanged] callback failed: {ex.GetType().Name} - {ex.Message}"));
    }
}
