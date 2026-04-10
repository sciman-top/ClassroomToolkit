using System;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void SaveCurrentPageIfNeeded()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
    }

    private void SaveCurrentPageOnNavigate(
        bool forceBackground,
        bool persistToSidecar = true,
        bool finalizeActiveOperation = true)
    {
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "save-enter",
                $"force={forceBackground} persist={persistToSidecar} dirty={IsCurrentPageDirty()}");
        }
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-skip", "scope!=photo");
            }
            return;
        }

        var hadActiveInkOperation = IsInkOperationActive();
        if (finalizeActiveOperation && hadActiveInkOperation)
        {
            FinalizeActiveInkOperation();
        }
        // Interactive seam switching prefers async persistence, but when cache is disabled
        // we must persist synchronously here to avoid losing the finalized previous-page stroke.
        var shouldPersistToSidecar = persistToSidecar || (!_inkCacheEnabled && hadActiveInkOperation);

        if (!forceBackground && !IsCurrentPageDirty())
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-skip", "not-dirty");
            }
            return;
        }
        var cacheKey = _currentCacheKey;

        // Interactive cross-page switching should avoid heavy clone/hash work on pointer-down.
        // When no active operation exists and cache already has current page snapshot, rely on
        // existing cache + delayed autosave to keep UI input path lightweight.
        if (!persistToSidecar
            && !forceBackground
            && !hadActiveInkOperation
            && _inkCacheEnabled
            && !string.IsNullOrWhiteSpace(cacheKey)
            && _photoCache.TryGet(cacheKey, out _))
        {
            ScheduleSidecarAutoSave();
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-fast-return", "cache-hit + autosave");
            }
            return;
        }

        List<InkStrokeData> strokes;
        var reusedCachedSnapshot = false;

        if (!persistToSidecar
            && !forceBackground
            && !hadActiveInkOperation
            && _inkCacheEnabled
            && !string.IsNullOrWhiteSpace(cacheKey)
            && _photoCache.TryGet(cacheKey, out var cachedStrokes))
        {
            strokes = cachedStrokes;
            reusedCachedSnapshot = true;
        }
        else
        {
            strokes = CloneCommittedInkStrokes();
        }

        if (_inkCacheEnabled && !string.IsNullOrWhiteSpace(cacheKey))
        {
            if (strokes.Count == 0)
            {
                _photoCache.Remove(cacheKey);
            }
            else if (!reusedCachedSnapshot)
            {
                _photoCache.Set(cacheKey, strokes);
                System.Diagnostics.Debug.WriteLine($"[InkCache] Saved {strokes.Count} strokes for key={cacheKey}");
            }
        }
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "save-cache-updated",
                $"strokes={strokes.Count} reused={reusedCachedSnapshot}");
        }

        if (shouldPersistToSidecar)
        {
            // Method A: also persist to sidecar file on disk
            PersistInkToSidecar(strokes, _currentDocumentPath, _currentPageIndex);
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("save-sidecar-sync");
            }
        }
        else
        {
            // Interactive cross-page input should avoid blocking IO on pointer down.
            // Persist old page snapshot asynchronously to keep consistency without UI stalls.
            if (_inkPersistence != null && _inkSaveEnabled && !string.IsNullOrWhiteSpace(_currentDocumentPath))
            {
                string snapshotHash = string.Empty;
                if (reusedCachedSnapshot
                    && _inkDirtyPages.TryGetRuntimeState(_currentDocumentPath, _currentPageIndex, out _, out var knownHash, out var dirty)
                    && dirty
                    && !string.IsNullOrWhiteSpace(knownHash))
                {
                    snapshotHash = knownHash;
                }
                else
                {
                    snapshotHash = ComputeInkHash(strokes);
                }
                var snapshot = new SidecarPersistSnapshot(
                    _inkPersistence,
                    _currentDocumentPath,
                    _currentPageIndex,
                    strokes,
                    snapshotHash);
                QueueSidecarAutoSave(snapshot);
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("save-sidecar-queued");
                }
            }
            else
            {
                ScheduleSidecarAutoSave();
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("save-sidecar-timer");
                }
            }
        }
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("save-exit");
        }
    }
}
