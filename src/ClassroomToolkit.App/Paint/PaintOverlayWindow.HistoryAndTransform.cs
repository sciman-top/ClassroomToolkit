using System;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    public void Undo()
    {
        if (InkUndoHistoryPolicy.ShouldPreferGlobalPhotoUndo(_photoModeActive, _globalInkHistory.Count))
        {
            if (TryUndoAcrossPages())
            {
                return;
            }
        }
        if (InkUndoHistoryPolicy.ShouldPreferLocalVectorUndo(_inkRecordEnabled, IsPhotoInkModeActive(), _inkHistory.Count))
        {
            var snapshot = _inkHistory[^1];
            _inkHistory.RemoveAt(_inkHistory.Count - 1);
            _inkStrokes.Clear();
            _inkStrokes.AddRange(CloneInkStrokes(snapshot.Strokes));
            RedrawInkSurface();
            NotifyInkStateChanged(updateActiveSnapshot: true);
            if (IsPhotoInkModeActive())
            {
                PersistUndoRestoredPhotoInkSnapshot(_currentDocumentPath, _currentPageIndex, _inkStrokes);
                RequestCrossPageDisplayUpdate(CrossPageUpdateSources.UndoSnapshot);
            }
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
        if (!TryApplyGlobalUndoSnapshot(snapshot))
        {
            return false;
        }
        _globalInkHistory.RemoveAt(_globalInkHistory.Count - 1);
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
            RemoveMatchingCurrentInkHistorySnapshot(snapshot, snapshotHash);
            PersistUndoRestoredPhotoInkSnapshot(_currentDocumentPath, _currentPageIndex, _inkStrokes);
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.UndoSnapshot);
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

            InvalidateNeighborInkCache(cacheKey);
        }

        MarkInkPageModified(snapshot.SourcePath, snapshot.PageIndex, snapshotHash, snapshotStrokes);
        PersistUndoRestoredPhotoInkSnapshot(snapshot.SourcePath, snapshot.PageIndex, snapshotStrokes);
        RequestCrossPageDisplayUpdate(CrossPageUpdateSources.UndoSnapshot);
        return true;
    }

    private void RemoveMatchingCurrentInkHistorySnapshot(GlobalInkSnapshot snapshot, string snapshotHash)
    {
        if (_inkHistory.Count == 0)
        {
            return;
        }

        var local = _inkHistory[^1];
        if (!string.Equals(local.SourcePath, snapshot.SourcePath, StringComparison.OrdinalIgnoreCase)
            || local.PageIndex != snapshot.PageIndex
            || !string.Equals(local.Hash, snapshotHash, StringComparison.Ordinal))
        {
            return;
        }

        _inkHistory.RemoveAt(_inkHistory.Count - 1);
    }

    private void PersistUndoRestoredPhotoInkSnapshot(
        string sourcePath,
        int pageIndex,
        IReadOnlyList<InkStrokeData> strokes)
    {
        if (!_photoModeActive || !_inkSaveEnabled || _inkPersistence == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        _inkSidecarAutoSaveTimer?.Stop();
        _inkSidecarAutoSaveGate.NextGeneration();
        PersistInkToSidecar(CloneInkStrokes(strokes), sourcePath, pageIndex);
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
        _lastPhotoScaleX = PhotoUnifiedTransformDefaults.NormalizeScale(scaleX);
        _lastPhotoScaleY = PhotoUnifiedTransformDefaults.NormalizeScale(scaleY);
        _lastPhotoTranslateX = PhotoUnifiedTransformDefaults.NormalizeTranslation(translateX);
        _lastPhotoTranslateY = PhotoUnifiedTransformDefaults.NormalizeTranslation(translateY);
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
        scaleX = PhotoUnifiedTransformDefaults.NormalizeScale(_lastPhotoScaleX);
        scaleY = PhotoUnifiedTransformDefaults.NormalizeScale(_lastPhotoScaleY);
        translateX = PhotoUnifiedTransformDefaults.NormalizeTranslation(_lastPhotoTranslateX);
        translateY = PhotoUnifiedTransformDefaults.NormalizeTranslation(_lastPhotoTranslateY);
        return true;
    }
}
