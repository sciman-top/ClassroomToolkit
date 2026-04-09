using System;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    public void Undo()
    {
        if (_inkRecordEnabled && _photoModeActive && _globalInkHistory.Count > 0)
        {
            if (TryUndoAcrossPages())
            {
                return;
            }
        }
        if (_inkRecordEnabled && _inkHistory.Count > 0)
        {
            var snapshot = _inkHistory[^1];
            _inkHistory.RemoveAt(_inkHistory.Count - 1);
            _inkStrokes.Clear();
            _inkStrokes.AddRange(CloneInkStrokes(snapshot.Strokes));
            RedrawInkSurface();
            NotifyInkStateChanged(updateActiveSnapshot: true);
            return;
        }
        if (_history.Count == 0)
        {
            return;
        }
        var rasterSnapshot = _history[^1];
        _history.RemoveAt(_history.Count - 1);
        RestoreSnapshot(rasterSnapshot);
    }

    private bool TryUndoAcrossPages()
    {
        if (_globalInkHistory.Count == 0)
        {
            return false;
        }

        var snapshot = _globalInkHistory[^1];
        _globalInkHistory.RemoveAt(_globalInkHistory.Count - 1);

        if (!TryApplyGlobalUndoSnapshot(snapshot))
        {
            return false;
        }
        return true;
    }

    private bool TryApplyGlobalUndoSnapshot(GlobalInkSnapshot snapshot)
    {
        if (!_photoModeActive || _currentCacheScope != InkCacheScope.Photo)
        {
            return false;
        }

        var snapshotStrokes = CloneInkStrokes(snapshot.Strokes);
        var snapshotHash = ComputeInkHash(snapshotStrokes);
        var cacheKey = snapshot.CacheKey ?? string.Empty;

        if (string.Equals(_currentCacheKey, snapshot.CacheKey, StringComparison.OrdinalIgnoreCase))
        {
            _inkStrokes.Clear();
            _inkStrokes.AddRange(snapshotStrokes);
            RedrawInkSurface();
            MarkInkPageModified(_currentDocumentPath, _currentPageIndex, snapshotHash, _inkStrokes);
            NotifyInkStateChanged(updateActiveSnapshot: true);
            return true;
        }

        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        if (_inkCacheEnabled)
        {
            if (snapshotStrokes.Count == 0)
            {
                _photoCache.Remove(cacheKey);
            }
            else
            {
                _photoCache.Set(cacheKey, snapshotStrokes);
            }
        }

        MarkInkPageModified(snapshot.SourcePath, snapshot.PageIndex, snapshotHash, snapshotStrokes);
        RequestCrossPageDisplayUpdate(CrossPageUpdateSources.UndoSnapshot);
        return true;
    }

    public void UpdateNeighborPrefetchRadiusMax(int maxRadius)
    {
        _neighborPrefetchRadiusMaxSetting = Math.Clamp(maxRadius, CrossPageNeighborPrefetchRadiusMin, CrossPageNeighborPrefetchRadiusMax);
    }

    public void SetPhotoUnifiedTransformState(bool enabled, double scaleX, double scaleY, double translateX, double translateY)
    {
        _photoUnifiedTransformReady = enabled && _rememberPhotoTransform;
        if (!_photoUnifiedTransformReady)
        {
            return;
        }
        _lastPhotoScaleX = scaleX;
        _lastPhotoScaleY = scaleY;
        _lastPhotoTranslateX = translateX;
        _lastPhotoTranslateY = translateY;
        _photoUserTransformDirty = true;
        if (PhotoUnifiedTransformApplyPolicy.ShouldApplyRuntimeTransform(
                _rememberPhotoTransform,
                IsPhotoInkModeActive(),
                IsCrossPageDisplayActive()))
        {
            ApplyLastUnifiedPhotoTransform(markUserDirty: true);
            UpdateCurrentPageWidthNormalization();
            RequestInkRedraw();
        }
    }

    public bool TryGetPhotoUnifiedTransformState(out double scaleX, out double scaleY, out double translateX, out double translateY)
    {
        if (!_photoUnifiedTransformReady)
        {
            scaleX = PhotoTransformViewportDefaults.DefaultScale;
            scaleY = PhotoTransformViewportDefaults.DefaultScale;
            translateX = PhotoUnifiedTransformDefaults.DefaultTranslateDip;
            translateY = PhotoUnifiedTransformDefaults.DefaultTranslateDip;
            return false;
        }
        scaleX = _lastPhotoScaleX;
        scaleY = _lastPhotoScaleY;
        translateX = _lastPhotoTranslateX;
        translateY = _lastPhotoTranslateY;
        return true;
    }
}
