using System;
using System.Collections.Generic;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct InkPageLoadExecutionResult(
    bool SkippedForNonPhotoScope,
    bool ClearedInkState,
    bool PurgedHiddenCurrentPage,
    bool AppliedCachedStrokes,
    bool LoadedFromSidecar,
    bool SkippedForEmptyCacheKey,
    int LoadedStrokeCount);

internal static class InkPageLoadCoordinator
{
    internal delegate bool TryGetCachedStrokesDelegate(string cacheKey, out List<InkStrokeData> strokes);

    internal static InkPageLoadExecutionResult Apply(
        bool photoCacheScopeActive,
        bool inkCacheEnabled,
        bool inkShowEnabled,
        string currentCacheKey,
        bool allowDiskFallback,
        bool hasInkPersistence,
        bool preferInteractiveFastPath,
        TryGetCachedStrokesDelegate tryGetCachedStrokes,
        Func<bool> tryLoadInkFromSidecar,
        Action purgePersistedInkForHiddenCurrentPage,
        Action clearInkSurfaceState,
        Action<IReadOnlyList<InkStrokeData>, bool> applyInkStrokes,
        Action<string, string?>? markTraceStage = null)
    {
        ArgumentNullException.ThrowIfNull(tryGetCachedStrokes);
        ArgumentNullException.ThrowIfNull(tryLoadInkFromSidecar);
        ArgumentNullException.ThrowIfNull(purgePersistedInkForHiddenCurrentPage);
        ArgumentNullException.ThrowIfNull(clearInkSurfaceState);
        ArgumentNullException.ThrowIfNull(applyInkStrokes);

        markTraceStage?.Invoke(
            "load-enter",
            $"allowDisk={allowDiskFallback} preferFast={preferInteractiveFastPath}");

        if (!photoCacheScopeActive)
        {
            markTraceStage?.Invoke("load-skip", "scope!=photo");
            return new InkPageLoadExecutionResult(
                SkippedForNonPhotoScope: true,
                ClearedInkState: false,
                PurgedHiddenCurrentPage: false,
                AppliedCachedStrokes: false,
                LoadedFromSidecar: false,
                SkippedForEmptyCacheKey: false,
                LoadedStrokeCount: 0);
        }

        if (!inkCacheEnabled)
        {
            clearInkSurfaceState();
            markTraceStage?.Invoke("load-clear", "cache-disabled");
            return new InkPageLoadExecutionResult(
                SkippedForNonPhotoScope: false,
                ClearedInkState: true,
                PurgedHiddenCurrentPage: false,
                AppliedCachedStrokes: false,
                LoadedFromSidecar: false,
                SkippedForEmptyCacheKey: false,
                LoadedStrokeCount: 0);
        }

        if (!inkShowEnabled)
        {
            purgePersistedInkForHiddenCurrentPage();
            clearInkSurfaceState();
            markTraceStage?.Invoke("load-clear", "ink-hidden");
            return new InkPageLoadExecutionResult(
                SkippedForNonPhotoScope: false,
                ClearedInkState: true,
                PurgedHiddenCurrentPage: true,
                AppliedCachedStrokes: false,
                LoadedFromSidecar: false,
                SkippedForEmptyCacheKey: false,
                LoadedStrokeCount: 0);
        }

        if (string.IsNullOrWhiteSpace(currentCacheKey))
        {
            markTraceStage?.Invoke("load-skip", "empty-cache-key");
            return new InkPageLoadExecutionResult(
                SkippedForNonPhotoScope: false,
                ClearedInkState: false,
                PurgedHiddenCurrentPage: false,
                AppliedCachedStrokes: false,
                LoadedFromSidecar: false,
                SkippedForEmptyCacheKey: true,
                LoadedStrokeCount: 0);
        }

        if (tryGetCachedStrokes(currentCacheKey, out var cached))
        {
            applyInkStrokes(cached, preferInteractiveFastPath);
            markTraceStage?.Invoke("load-cache-hit", $"strokes={cached.Count}");
            return new InkPageLoadExecutionResult(
                SkippedForNonPhotoScope: false,
                ClearedInkState: false,
                PurgedHiddenCurrentPage: false,
                AppliedCachedStrokes: true,
                LoadedFromSidecar: false,
                SkippedForEmptyCacheKey: false,
                LoadedStrokeCount: cached.Count);
        }

        if (InkPersistenceTogglePolicy.ShouldLoadPersistedInk(allowDiskFallback)
            && hasInkPersistence
            && tryLoadInkFromSidecar())
        {
            markTraceStage?.Invoke("load-sidecar-hit", null);
            return new InkPageLoadExecutionResult(
                SkippedForNonPhotoScope: false,
                ClearedInkState: false,
                PurgedHiddenCurrentPage: false,
                AppliedCachedStrokes: false,
                LoadedFromSidecar: true,
                SkippedForEmptyCacheKey: false,
                LoadedStrokeCount: 0);
        }

        clearInkSurfaceState();
        markTraceStage?.Invoke("load-clear", "cache-miss");
        return new InkPageLoadExecutionResult(
            SkippedForNonPhotoScope: false,
            ClearedInkState: true,
            PurgedHiddenCurrentPage: false,
            AppliedCachedStrokes: false,
            LoadedFromSidecar: false,
            SkippedForEmptyCacheKey: false,
            LoadedStrokeCount: 0);
    }
}
