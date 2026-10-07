using ClassroomToolkit.App.Ink;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System;

namespace ClassroomToolkit.App.Paint;

internal sealed class InkDirtyPageCoordinator
{
    private readonly object _stateGate = new();
    private readonly Dictionary<string, InkPageRuntimeState> _pageStates = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _sessionModifiedPages = new(StringComparer.OrdinalIgnoreCase);

    internal void MarkLoaded(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        lock (_stateGate)
        {
            var state = GetOrCreate(sourcePath, pageIndex);
            state.Loaded = true;
            state.Dirty = false;
            state.LastKnownHash = hash;
            state.LastSavedHash = hash;
        }
    }

    internal void MarkModified(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        var key = BuildRuntimePageStateKey(sourcePath, pageIndex);
        lock (_stateGate)
        {
            var state = GetOrCreate(sourcePath, pageIndex);
            state.Loaded = true;
            state.Dirty = true;
            state.Version++;
            state.LastKnownHash = hash;
            _sessionModifiedPages.Add(key);
        }
    }

    internal void MarkPersisted(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }

        lock (_stateGate)
        {
            var state = GetOrCreate(sourcePath, pageIndex);
            state.Loaded = true;
            state.Dirty = false;
            state.LastKnownHash = hash;
            state.LastSavedHash = hash;
        }
    }

    internal bool MarkPersistedIfUnchanged(string sourcePath, int pageIndex, string hash)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        lock (_stateGate)
        {
            var state = GetOrCreate(sourcePath, pageIndex);
            if (!string.IsNullOrWhiteSpace(state.LastKnownHash)
                && !string.Equals(state.LastKnownHash, hash, StringComparison.Ordinal))
            {
                return false;
            }

            state.Loaded = true;
            state.Dirty = false;
            state.LastKnownHash = hash;
            state.LastSavedHash = hash;
            return true;
        }
    }

    internal bool IsDirty(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        var key = BuildRuntimePageStateKey(sourcePath, pageIndex);
        lock (_stateGate)
        {
            return _pageStates.TryGetValue(key, out var state) && state.Dirty;
        }
    }

    internal bool WasModifiedInSession(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        lock (_stateGate)
        {
            return _sessionModifiedPages.Contains(BuildRuntimePageStateKey(sourcePath, pageIndex));
        }
    }

    internal IEnumerable<string> EnumerateSessionModifiedSourcesInDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            yield break;
        }

        string[] snapshot;
        lock (_stateGate)
        {
            snapshot = _sessionModifiedPages.ToArray();
        }

        foreach (var runtimeKey in snapshot)
        {
            if (!TryParseRuntimePageStateKey(runtimeKey, out var sourcePath, out _))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                continue;
            }
            if (!string.Equals(Path.GetDirectoryName(sourcePath), directoryPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            yield return sourcePath;
        }
    }

    internal IReadOnlyList<(string SourcePath, int PageIndex)> GetDirtyPages(string? directoryPath)
    {
        var result = new List<(string SourcePath, int PageIndex)>();
        lock (_stateGate)
        {
            foreach (var entry in _pageStates)
            {
                if (!entry.Value.Dirty)
                {
                    continue;
                }

                if (!TryParseRuntimePageStateKey(entry.Key, out var sourcePath, out var pageIndex))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(directoryPath) &&
                    !string.Equals(Path.GetDirectoryName(sourcePath), directoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                result.Add((sourcePath, pageIndex));
            }
        }

        return result;
    }

    internal bool TryGetRuntimeState(string sourcePath, int pageIndex, out int version, out string lastKnownHash, out bool dirty)
    {
        version = 0;
        lastKnownHash = string.Empty;
        dirty = false;
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        var key = BuildRuntimePageStateKey(sourcePath, pageIndex);
        lock (_stateGate)
        {
            if (!_pageStates.TryGetValue(key, out var state))
            {
                return false;
            }

            version = state.Version;
            lastKnownHash = state.LastKnownHash;
            dirty = state.Dirty;
            return true;
        }
    }

    private InkPageRuntimeState GetOrCreate(string sourcePath, int pageIndex)
    {
        var key = BuildRuntimePageStateKey(sourcePath, pageIndex);
        if (!_pageStates.TryGetValue(key, out var state))
        {
            state = new InkPageRuntimeState();
            _pageStates[key] = state;
        }

        return state;
    }

    internal static string BuildRuntimePageStateKey(string sourcePath, int pageIndex)
    {
        var normalizedSourcePath = NormalizeSourcePath(sourcePath);
        return $"src|{normalizedSourcePath}|page|{pageIndex}";
    }

    internal static bool TryParseRuntimePageStateKey(string runtimeKey, out string sourcePath, out int pageIndex)
    {
        sourcePath = string.Empty;
        pageIndex = 0;
        if (string.IsNullOrWhiteSpace(runtimeKey) || !runtimeKey.StartsWith("src|", StringComparison.Ordinal))
        {
            return false;
        }

        const string marker = "|page|";
        var markerIndex = runtimeKey.LastIndexOf(marker, StringComparison.Ordinal);
        if (markerIndex <= "src|".Length)
        {
            return false;
        }

        sourcePath = runtimeKey["src|".Length..markerIndex];
        var pageRaw = runtimeKey[(markerIndex + marker.Length)..];
        if (!int.TryParse(pageRaw, out pageIndex) || pageIndex <= 0)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(sourcePath);
    }

    private static string NormalizeSourcePath(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(sourcePath);
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            return sourcePath;
        }
    }

    private sealed class InkPageRuntimeState
    {
        public bool Loaded { get; set; }
        public bool Dirty { get; set; }
        public int Version { get; set; }
        public string LastKnownHash { get; set; } = string.Empty;
        public string LastSavedHash { get; set; } = string.Empty;
    }
}

internal static class InkDirtyPageFlushCoordinator
{
    internal delegate bool TryGetPageStrokesDelegate(string sourcePath, int pageIndex, out List<InkStrokeData> strokes);
    internal delegate bool PersistPageDelegate(string sourcePath, int pageIndex, List<InkStrokeData> strokes, out string? errorMessage);

    internal sealed class FlushResult
    {
        public int AttemptedCount { get; set; }
        public int SucceededCount { get; set; }
        public List<(string SourcePath, int PageIndex, string Error)> Failures { get; } = new();
        public bool IsSuccess => Failures.Count == 0;
    }

    internal static FlushResult Flush(
        bool inkSaveEnabled,
        string? directoryPath,
        Action stopAutoSaveTimer,
        Action cancelAutoSaveGeneration,
        Action finalizeActiveOperation,
        Func<string?, IReadOnlyList<(string SourcePath, int PageIndex)>> getDirtyPages,
        TryGetPageStrokesDelegate tryGetPageStrokes,
        PersistPageDelegate persistPage)
    {
        var result = new FlushResult();
        if (!inkSaveEnabled)
        {
            return result;
        }

        stopAutoSaveTimer();
        cancelAutoSaveGeneration();
        finalizeActiveOperation();

        foreach (var (sourcePath, pageIndex) in getDirtyPages(directoryPath))
        {
            if (!tryGetPageStrokes(sourcePath, pageIndex, out var strokes))
            {
                continue;
            }

            result.AttemptedCount++;
            if (persistPage(sourcePath, pageIndex, strokes, out var errorMessage))
            {
                result.SucceededCount++;
                continue;
            }

            result.Failures.Add((sourcePath, pageIndex, errorMessage ?? "unknown-error"));
        }

        return result;
    }
}

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

        if (InkPersistencePolicies.ShouldLoadPersistedInk(allowDiskFallback)
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
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkPageLoadCoordinator] trace callback failed: {ex.GetType().Name} - {ex.Message}");
        }
    }
}

internal readonly record struct InkShowTransitionExecutionResult(
    bool AppliedSetting,
    bool ReturnedAfterSetting,
    bool ClearedInkState,
    bool LoadedCurrentPage,
    bool RequestedCrossPageUpdate);

internal static class InkShowTransitionCoordinator
{
    internal static InkShowTransitionExecutionResult Apply(
        bool currentInkShowEnabled,
        bool requestedEnabled,
        bool photoModeActive,
        Action<bool> setInkShowEnabled,
        Action purgePersistedInkForHiddenCurrentDocument,
        Action clearInkSurfaceState,
        Action clearNeighborInkVisuals,
        Action clearNeighborInkCache,
        Action clearNeighborInkRenderPending,
        Action clearNeighborInkSidecarLoadPending,
        Action loadCurrentPageIfExists,
        Action<string> requestCrossPageDisplayUpdate)
    {
        ArgumentNullException.ThrowIfNull(setInkShowEnabled);
        ArgumentNullException.ThrowIfNull(purgePersistedInkForHiddenCurrentDocument);
        ArgumentNullException.ThrowIfNull(clearInkSurfaceState);
        ArgumentNullException.ThrowIfNull(clearNeighborInkVisuals);
        ArgumentNullException.ThrowIfNull(clearNeighborInkCache);
        ArgumentNullException.ThrowIfNull(clearNeighborInkRenderPending);
        ArgumentNullException.ThrowIfNull(clearNeighborInkSidecarLoadPending);
        ArgumentNullException.ThrowIfNull(loadCurrentPageIfExists);
        ArgumentNullException.ThrowIfNull(requestCrossPageDisplayUpdate);

        var transitionPlan = InkPersistencePolicies.ResolveInkShowUpdateTransition(
            currentInkShowEnabled,
            requestedEnabled,
            photoModeActive);
        if (!transitionPlan.ShouldApplySetting)
        {
            return default;
        }

        PaintActionInvoker.TryInvoke(() => setInkShowEnabled(requestedEnabled));
        if (transitionPlan.ShouldReturnAfterSetting)
        {
            return new InkShowTransitionExecutionResult(
                AppliedSetting: true,
                ReturnedAfterSetting: true,
                ClearedInkState: false,
                LoadedCurrentPage: false,
                RequestedCrossPageUpdate: false);
        }

        if (transitionPlan.ShouldClearInkState)
        {
            PaintActionInvoker.TryInvoke(purgePersistedInkForHiddenCurrentDocument);
            PaintActionInvoker.TryInvoke(clearInkSurfaceState);
            PaintActionInvoker.TryInvoke(clearNeighborInkVisuals);
            PaintActionInvoker.TryInvoke(clearNeighborInkCache);
            PaintActionInvoker.TryInvoke(clearNeighborInkRenderPending);
            PaintActionInvoker.TryInvoke(clearNeighborInkSidecarLoadPending);
            if (transitionPlan.RequestCrossPageUpdateForDisabled)
            {
                PaintActionInvoker.TryInvoke(() => requestCrossPageDisplayUpdate(CrossPageUpdateSources.InkShowDisabled));
            }

            return new InkShowTransitionExecutionResult(
                AppliedSetting: true,
                ReturnedAfterSetting: false,
                ClearedInkState: true,
                LoadedCurrentPage: false,
                RequestedCrossPageUpdate: transitionPlan.RequestCrossPageUpdateForDisabled);
        }

        if (transitionPlan.ShouldLoadCurrentPage)
        {
            PaintActionInvoker.TryInvoke(loadCurrentPageIfExists);
        }

        if (transitionPlan.RequestCrossPageUpdateForEnabled)
        {
            PaintActionInvoker.TryInvoke(() => requestCrossPageDisplayUpdate(CrossPageUpdateSources.InkShowEnabled));
        }

        return new InkShowTransitionExecutionResult(
            AppliedSetting: true,
            ReturnedAfterSetting: false,
            ClearedInkState: false,
            LoadedCurrentPage: transitionPlan.ShouldLoadCurrentPage,
            RequestedCrossPageUpdate: transitionPlan.RequestCrossPageUpdateForEnabled);
    }

}

internal readonly record struct InkStrokeApplyExecutionResult(
    int AppliedStrokeCount,
    bool UsedInteractiveFastPathCopy,
    bool RedrewInkSurface,
    bool MarkedCurrentPageLoaded);

internal static class InkStrokeApplyCoordinator
{
    internal static InkStrokeApplyExecutionResult Apply(
        IReadOnlyList<InkStrokeData> strokes,
        bool preferInteractiveFastPath,
        Action clearRuntimeStrokes,
        Action<IReadOnlyList<InkStrokeData>> addRuntimeStrokes,
        Func<IReadOnlyList<InkStrokeData>, bool, bool> tryApplyNeighborInkBitmapForCurrentPage,
        Action redrawInkSurface,
        Action finalizeFastAppliedInkSurface,
        Action markCurrentInkPageLoaded,
        Action<double, bool> recordPerfMilliseconds,
        Func<double> getElapsedMilliseconds,
        Func<bool> dispatcherCheckAccess,
        Action<string, string?>? markTraceStage = null)
    {
        ArgumentNullException.ThrowIfNull(strokes);
        ArgumentNullException.ThrowIfNull(clearRuntimeStrokes);
        ArgumentNullException.ThrowIfNull(addRuntimeStrokes);
        ArgumentNullException.ThrowIfNull(tryApplyNeighborInkBitmapForCurrentPage);
        ArgumentNullException.ThrowIfNull(redrawInkSurface);
        ArgumentNullException.ThrowIfNull(finalizeFastAppliedInkSurface);
        ArgumentNullException.ThrowIfNull(markCurrentInkPageLoaded);
        ArgumentNullException.ThrowIfNull(recordPerfMilliseconds);
        ArgumentNullException.ThrowIfNull(getElapsedMilliseconds);
        ArgumentNullException.ThrowIfNull(dispatcherCheckAccess);

        InvokeTraceStageSafely(
            markTraceStage,
            "apply-enter",
            $"strokes={strokes.Count} preferFast={preferInteractiveFastPath}");

        clearRuntimeStrokes();
        addRuntimeStrokes(strokes);

        var fastApplied = tryApplyNeighborInkBitmapForCurrentPage(strokes, preferInteractiveFastPath);
        if (!fastApplied)
        {
            InvokeTraceStageSafely(markTraceStage, "apply-redraw", null);
            redrawInkSurface();
        }
        else
        {
            InvokeTraceStageSafely(markTraceStage, "apply-fast-bitmap", null);
            finalizeFastAppliedInkSurface();
        }

        markCurrentInkPageLoaded();
        recordPerfMilliseconds(getElapsedMilliseconds(), dispatcherCheckAccess());
        InvokeTraceStageSafely(markTraceStage, "apply-exit", $"ms={getElapsedMilliseconds():F2}");

        return new InkStrokeApplyExecutionResult(
            AppliedStrokeCount: strokes.Count,
            UsedInteractiveFastPathCopy: fastApplied,
            RedrewInkSurface: !fastApplied,
            MarkedCurrentPageLoaded: true);
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
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            Debug.WriteLine($"[InkStrokeApplyCoordinator] trace callback failed: {ex.GetType().Name} - {ex.Message}");
        }
    }
}
