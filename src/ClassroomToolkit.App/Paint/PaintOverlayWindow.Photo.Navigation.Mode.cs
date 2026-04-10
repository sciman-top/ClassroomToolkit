using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Controls.Primitives;
using System.Windows.Shell;
using System.Windows.Interop;
using System.Diagnostics;
using System.IO;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;
using ClassroomToolkit.App.Paint.Brushes;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    public void SetPhotoSequence(IReadOnlyList<string> paths, int currentIndex)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var normalized = PhotoCrossPageSequencePolicy.Normalize(paths, currentIndex);
        _photoSequencePaths = normalized.Sequence.ToList();
        _photoSequenceIndex = normalized.CurrentIndex;
        ClearNeighborImageCache();
    }

    public bool IsPhotoModeActive => _photoModeActive;
    public bool IsPhotoFullscreenActive => _photoModeActive && _photoFullscreen;

    public void EnterPhotoMode(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }
        _photoUnboundedInkCanvasEnabled = false;
        ResetCrossPageReplayState();
        _crossPageUpdateDeferredByInkInput = false;
        RecoverInkWalForDirectory(sourcePath);
        _foregroundPhotoActive = false;
        var reentryPlan = PhotoOverlayReentryPolicy.Resolve(
            windowMinimized: WindowState == WindowState.Minimized,
            photoModeActive: _photoModeActive,
            sameSourcePath: string.Equals(_currentDocumentPath, sourcePath, StringComparison.OrdinalIgnoreCase));
        WindowStateNormalizationExecutor.Apply(this, reentryPlan.NormalizeWindowState);
        if (reentryPlan.ReturnEarly)
        {
            OverlayFocusExecutionExecutor.Apply(
                this,
                reentryPlan.ActivateOverlay,
                shouldKeyboardFocus: false);
            return;
        }
        var wasFullscreen = _photoModeActive ? _photoFullscreen : true;
        var wasPresentationFullscreen = false;
        if (!_photoModeActive && (_presentationOptions.AllowOffice || _presentationOptions.AllowWps))
        {
            var target = _presentationResolver.ResolvePresentationTarget(
                _presentationClassifier,
                _presentationOptions.AllowWps,
                _presentationOptions.AllowOffice,
                _currentProcessId);
            wasPresentationFullscreen = IsFullscreenPresentationWindow(target);
        }
        if (_photoModeActive)
        {
            SaveCurrentPageOnNavigate(forceBackground: false);
        }
        else if (wasPresentationFullscreen || _presentationFullscreenActive)
        {
            _presentationFullscreenActive = false;
            ClearCurrentPresentationType();
            _currentCacheScope = InkCacheScope.None;
            _currentCacheKey = string.Empty;
            ClearInkSurfaceState();
        }
        else if (!_photoModeActive && (_inkStrokes.Count > 0 || _hasDrawing))
        {
            ClearInkSurfaceState();
        }
        var isPdf = IsPdfFile(sourcePath);
        if (_photoModeActive && _photoDocumentIsPdf)
        {
            ClosePdfDocument();
        }
        _currentPageIndex = 1;
        var hadUserTransformDirty = _photoUserTransformDirty;
        _photoUserTransformDirty = false;
        EnsurePhotoTransformsWritable();
        var transformInitPlan = PhotoEnterTransformInitPolicy.Resolve(
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled(),
            rememberPhotoTransform: _rememberPhotoTransform,
            photoUnifiedTransformReady: _photoUnifiedTransformReady,
            hadUserTransformDirty: hadUserTransformDirty);
        if (transformInitPlan.ShouldApplyUnifiedTransform)
        {
            ApplyLastUnifiedPhotoTransform(markUserDirty: transformInitPlan.ShouldMarkUserDirtyAfterUnifiedApply);
            if (transformInitPlan.ShouldMarkUnifiedTransformReady)
            {
                _photoUnifiedTransformReady = true;
            }
        }
        else if (transformInitPlan.ShouldTryStoredTransform)
        {
            var initialKey = BuildPhotoModeCacheKey(sourcePath, _currentPageIndex, isPdf);
            if (!TryApplyStoredPhotoTransform(initialKey))
            {
                ApplyIdentityPhotoTransform();
            }
        }
        else if (transformInitPlan.ShouldResetIdentity)
        {
            ApplyIdentityPhotoTransform();
        }
        _photoModeActive = true;
        UpdatePhotoContentTransforms(enabled: true);
        _photoFullscreen = wasFullscreen;
        _photoRestoreFullscreenPending = false;
        _presentationFullscreenActive = false;
        ClearCurrentPresentationType();
        EnsureOverlayTopmost(enforceZOrder: false);
        _currentCourseDate = DateTime.Today;
        _currentDocumentName = IoPath.GetFileNameWithoutExtension(sourcePath);
        _currentDocumentPath = sourcePath;
        ResetCrossPageNormalizedWidth();
        _currentCacheScope = InkCacheScope.Photo;
        _currentCacheKey = BuildPhotoModeCacheKey(sourcePath, _currentPageIndex, isPdf);
        _photoDocumentIsPdf = isPdf;
        _globalInkHistory.Clear();
        SetPhotoWindowMode(_photoFullscreen);
        UpdateWpsNavHookState();
        UpdatePresentationFocusMonitor();
        HidePhotoLoadingOverlay();
        if (isPdf)
        {
            ClosePdfDocument();
            ShowPhotoLoadingOverlay("正在加载PDF...");
            StartPdfOpenAsync(sourcePath);
        }
        else
        {
            if (!TrySetPhotoBackground(sourcePath))
            {
                HidePhotoLoadingOverlay();
                ExitPhotoMode();
                return;
            }
        }
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoModeChanged?.Invoke(true),
            ex => Debug.WriteLine($"[PhotoModeChanged] enter callback failed: {ex.GetType().Name} - {ex.Message}"));
        if (PhotoTitleText != null)
        {
            PhotoTitleText.Text = IoPath.GetFileName(sourcePath);
        }
        SafeActionExecutionExecutor.TryExecute(
            () => InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate),
            ex => Debug.WriteLine($"[InkContextChanged] enter callback failed: {ex.GetType().Name} - {ex.Message}"));
        ResetInkHistory();
        LoadCurrentPageIfExists();
        if (IsCrossPageDisplayActive())
        {
            UpdateCrossPageDisplay();
        }
        DispatchSessionEvent(new EnterPhotoFullscreenEvent(MapPhotoSource(isPdf)));
    }

    public void ExitPhotoMode()
    {
        if (!_photoModeActive)
        {
            return;
        }
        ResetCrossPageReplayState();
        _crossPageUpdateDeferredByInkInput = false;
        Interlocked.Increment(ref _photoLoadToken);
        HidePhotoLoadingOverlay();
        _foregroundPhotoActive = false;
        FlushPhotoTransformSave();
        SaveCurrentPageOnNavigate(forceBackground: false);
        if (!InkPersistenceTogglePolicy.ShouldRetainRuntimeCacheOnPhotoExit(_inkSaveEnabled))
        {
            EvictRuntimeInkCacheForClosedPhotoSession();
        }
        PhotoBackground.Source = null;
        RefreshPhotoBackgroundVisibility();
        _photoPageScale.ScaleX = 1.0;
        _photoPageScale.ScaleY = 1.0;
        ResetCrossPageNormalizedWidth();
        ClearNeighborPages();
        ClosePdfDocument();
        if (!_rememberPhotoTransform)
        {
            EnsurePhotoTransformsWritable();
            ApplyIdentityPhotoTransform();
            _photoUserTransformDirty = false;
        }
        _photoModeActive = false;
        _photoUnboundedInkCanvasEnabled = false;
        _boardSuspendedPhotoCache = false;
        UpdatePhotoContentTransforms(enabled: false);
        _photoFullscreen = false;
        _photoRestoreFullscreenPending = false;
        _photoDocumentIsPdf = false;
        SetPhotoWindowMode(fullscreen: false);
        UpdateWpsNavHookState();
        UpdatePresentationFocusMonitor();
        UpdateOverlayHitTestVisibility();
        UpdateInputPassthrough();
        EnsureOverlayTopmost(enforceZOrder: false);
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoModeChanged?.Invoke(false),
            ex => Debug.WriteLine($"[PhotoModeChanged] exit callback failed: {ex.GetType().Name} - {ex.Message}"));
        _currentDocumentName = string.Empty;
        _currentDocumentPath = string.Empty;
        if (PhotoTitleText != null)
        {
            PhotoTitleText.Text = "图片应用";
        }
        _currentPageIndex = 1;
        _currentCacheScope = InkCacheScope.None;
        _currentCacheKey = string.Empty;
        _globalInkHistory.Clear();
        ClearInkSurfaceState();
        DispatchSessionEvent(new ExitPhotoFullscreenEvent());
    }

    public void EnsurePhotoWindowedMode()
    {
        if (!_photoModeActive || !_photoFullscreen)
        {
            return;
        }

        _photoFullscreen = false;
        SetPhotoWindowMode(fullscreen: false);
    }

    public void SetPhotoInkCanvasUnbounded(bool enabled)
    {
        if (_photoUnboundedInkCanvasEnabled == enabled)
        {
            return;
        }

        _photoUnboundedInkCanvasEnabled = enabled;
        UpdatePhotoInkClip();
        if (IsPhotoInkModeActive())
        {
            MarkInkTransformVersionDirty();
            RequestInkRedraw();
        }
    }

    private void EvictRuntimeInkCacheForClosedPhotoSession()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return;
        }

        var sourcePathSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            _currentDocumentPath
        };
        if (!_photoDocumentIsPdf)
        {
            foreach (var sequencePath in _photoSequencePaths)
            {
                if (!string.IsNullOrWhiteSpace(sequencePath))
                {
                    sourcePathSet.Add(sequencePath);
                }
            }
        }

        foreach (var (cacheKey, _) in _photoCache.Snapshot())
        {
            if (!InkExportSnapshotBuilder.TryParseCacheKey(cacheKey, out var sourcePath, out var pageIndex))
            {
                continue;
            }
            if (!sourcePathSet.Contains(sourcePath))
            {
                continue;
            }

            _photoCache.Remove(cacheKey);
            ClearInkWalSnapshot(sourcePath, pageIndex);
        }
    }
}
