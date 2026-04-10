using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    /// <summary>
    /// Save current ink strokes to sidecar file (Method A).
    /// Called on page navigation, file close, and app exit.
    /// </summary>
    private void PersistInkToSidecar(
        List<InkStrokeData>? preparedStrokes = null,
        string? preparedSourcePath = null,
        int? preparedPageIndex = null)
    {
        _ = TryPersistInkToSidecarStrict(preparedStrokes, preparedSourcePath, preparedPageIndex, out _);
    }

    private bool TryPersistInkToSidecarStrict(
        List<InkStrokeData>? preparedStrokes,
        string? preparedSourcePath,
        int? preparedPageIndex,
        out string? errorMessage)
    {
        errorMessage = null;
        var persistence = _inkPersistence;
        if (persistence == null || !_inkSaveEnabled)
        {
            return true;
        }

        var sourcePath = string.IsNullOrWhiteSpace(preparedSourcePath) ? _currentDocumentPath : preparedSourcePath;
        var pageIndex = preparedPageIndex ?? _currentPageIndex;
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return true;
        }

        string? localError = null;
        var persisted = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                var strokes = preparedStrokes;
                if (strokes == null)
                {
                    FinalizeActiveInkOperation();
                    strokes = CloneCommittedInkStrokes();
                }

                var hash = ComputeInkHash(strokes);
                PersistInkHistorySnapshot(sourcePath, pageIndex, strokes, persistence);
                var persistedStrokes = LoadInkHistorySnapshot(sourcePath, pageIndex, persistence);
                var persistedHash = ComputeInkHash(persistedStrokes);
                if (!string.Equals(hash, persistedHash, StringComparison.Ordinal))
                {
                    localError = $"hash-mismatch expected={hash} actual={persistedHash}";
                    TrackInkWalSnapshot(sourcePath, pageIndex, strokes, hash);
                    return false;
                }

                MarkInkPagePersistedIfUnchanged(sourcePath, pageIndex, hash);
                if (strokes.Count == 0 && _inkExport != null)
                {
                    _inkExport.RemoveCompositeOutputsForPage(sourcePath, pageIndex);
                }

                PurgePersistedInkForHiddenSourceIfNeeded(sourcePath);
                _inkDiagnostics?.OnSyncPersist();
                System.Diagnostics.Debug.WriteLine($"[InkPersist] Saved {strokes.Count} strokes for page {pageIndex} of {sourcePath}");
                return true;
            },
            fallback: false,
            onFailure: ex =>
            {
                localError = ex.Message;
                var fallbackStrokes = preparedStrokes ?? new List<InkStrokeData>();
                TrackInkWalSnapshot(sourcePath, pageIndex, fallbackStrokes, ComputeInkHash(fallbackStrokes));
                System.Diagnostics.Debug.WriteLine($"[InkPersist] Save failed: {ex.Message}");
            });
        errorMessage = localError;
        return persisted;
    }

    private void ScheduleSidecarAutoSave()
    {
        if (!_inkSaveEnabled || _inkSidecarAutoSaveTimer == null)
        {
            return;
        }

        _inkSidecarAutoSaveTimer.Stop();
        _inkSidecarAutoSaveTimer.Start();
    }

    private bool TryCaptureSidecarPersistSnapshot(bool requireDirty, out SidecarPersistSnapshot? snapshot)
    {
        snapshot = null;
        var persistence = _inkPersistence;
        if (persistence == null || !_inkSaveEnabled)
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return false;
        }
        if (requireDirty && !IsCurrentPageDirty())
        {
            return false;
        }

        // Never finalize/capture while user is actively drawing/erasing.
        // Otherwise auto-save may truncate the in-flight stroke and cause visible "missing ink".
        if (IsInkOperationActive())
        {
            _inkDiagnostics?.OnAutoSaveDeferred("capture-active-operation");
            return false;
        }

        FinalizeActiveInkOperation();
        snapshot = new SidecarPersistSnapshot(
            persistence,
            _currentDocumentPath,
            _currentPageIndex,
            CloneCommittedInkStrokes(),
            ComputeInkHash(_inkStrokes));
        return true;
    }

    private void QueueSidecarAutoSave(SidecarPersistSnapshot snapshot)
    {
        var generation = _inkSidecarAutoSaveGate.NextGeneration();
        _ = _inkSidecarAutoSaveGate.RunAsync(generation, async isCurrent =>
        {
            if (!_inkSaveEnabled)
            {
                return;
            }

            for (int attempt = 1; attempt <= InkSidecarAutoSaveRetryMax; attempt++)
            {
                if (!isCurrent() || !_inkSaveEnabled)
                {
                    return;
                }

                var runtimeStateKnown = _inkDirtyPages.TryGetRuntimeState(
                    snapshot.SourcePath,
                    snapshot.PageIndex,
                    out _,
                    out var runtimeHash,
                    out _);
                if (!InkAutoSaveSnapshotAdmissionPolicy.ShouldPersistSnapshot(
                        runtimeStateKnown,
                        runtimeHash,
                        snapshot.SnapshotHash))
                {
                    _inkDiagnostics?.OnAutoSaveDeferred("stale-runtime-snapshot");
                    return;
                }

                if (TryPersistSidecarSnapshot(snapshot, logFailure: attempt == InkSidecarAutoSaveRetryMax))
                {
                    DispatchExportUiUpdate("autosave-persisted", () =>
                    {
                        var persisted = MarkInkPagePersistedIfUnchanged(
                            snapshot.SourcePath,
                            snapshot.PageIndex,
                            snapshot.SnapshotHash);
                        PurgePersistedInkForHiddenSourceIfNeeded(snapshot.SourcePath);
                        _inkDiagnostics?.OnAutoSavePersistResult(persisted);
                    });
                    return;
                }

                if (attempt >= InkSidecarAutoSaveRetryMax)
                {
                    break;
                }

                if (!await TryDelayAutoSaveRetryAsync(InkSidecarAutoSaveRetryDelayMs * attempt).ConfigureAwait(false))
                {
                    return;
                }
            }

            if (!isCurrent() || !_inkSaveEnabled)
            {
                return;
            }

            DispatchExportUiUpdate("autosave-failed-reschedule", () =>
            {
                MarkInkPageModified(
                    snapshot.SourcePath,
                    snapshot.PageIndex,
                    snapshot.SnapshotHash,
                    snapshot.Strokes);
                _inkDiagnostics?.OnAutoSaveFailure();
                ScheduleSidecarAutoSave();
            });
        });
    }

    private static async Task<bool> TryDelayAutoSaveRetryAsync(int delayMs)
    {
        try
        {
            await Task.Delay(delayMs).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine(
                $"[InkPersist] Auto-save retry delay interrupted: {ex.GetType().Name} - {ex.Message}");
            return false;
        }
    }

    private bool TryPersistSidecarSnapshot(SidecarPersistSnapshot snapshot, bool logFailure)
    {
        return SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                PersistInkHistorySnapshot(
                    snapshot.SourcePath,
                    snapshot.PageIndex,
                    snapshot.Strokes,
                    snapshot.Persistence);
                if (snapshot.Strokes.Count == 0 && _inkExport != null)
                {
                    _inkExport.RemoveCompositeOutputsForPage(snapshot.SourcePath, snapshot.PageIndex);
                }

                return true;
            },
            fallback: false,
            onFailure: ex =>
            {
                if (logFailure)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[InkPersist] Auto-save failed after retries: source={snapshot.SourcePath}, page={snapshot.PageIndex}, error={ex.Message}");
                }
            });
    }
}
