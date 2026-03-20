using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        InvokeTraceStageSafely(
            markTraceStage,
            "load-enter",
            $"allowDisk={allowDiskFallback} preferFast={preferInteractiveFastPath}");

        if (!photoCacheScopeActive)
        {
            InvokeTraceStageSafely(markTraceStage, "load-skip", "scope!=photo");
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
            InvokeTraceStageSafely(markTraceStage, "load-clear", "cache-disabled");
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
            InvokeTraceStageSafely(markTraceStage, "load-clear", "ink-hidden");
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
            InvokeTraceStageSafely(markTraceStage, "load-skip", "empty-cache-key");
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
            InvokeTraceStageSafely(markTraceStage, "load-cache-hit", $"strokes={cached.Count}");
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
            InvokeTraceStageSafely(markTraceStage, "load-sidecar-hit", null);
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
        InvokeTraceStageSafely(markTraceStage, "load-clear", "cache-miss");
        return new InkPageLoadExecutionResult(
            SkippedForNonPhotoScope: false,
            ClearedInkState: true,
            PurgedHiddenCurrentPage: false,
            AppliedCachedStrokes: false,
            LoadedFromSidecar: false,
            SkippedForEmptyCacheKey: false,
            LoadedStrokeCount: 0);
    }

    private static void InvokeTraceStageSafely(
        Action<string, string?>? markTraceStage,
        string stage,
        string? detail)
    {
        if (markTraceStage is null)
        {
            return;
        }

        try
        {
            markTraceStage(stage, detail);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkPageLoadCoordinator] trace callback failed: {ex.GetType().Name} - {ex.Message}");
        }
    }
}
