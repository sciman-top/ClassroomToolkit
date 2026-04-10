using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool TryLoadSidecarPageInkToCache(
        string sourcePath,
        int pageIndex,
        out List<InkStrokeData> strokes,
        bool allowWhenSaveDisabled = false)
    {
        strokes = new List<InkStrokeData>();
        if (_inkPersistence == null || string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }
        if (!allowWhenSaveDisabled && !_inkSaveEnabled)
        {
            return false;
        }

        var loadResult = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                var loaded = LoadInkHistorySnapshot(sourcePath, pageIndex, _inkPersistence);
                if (loaded == null || loaded.Count == 0)
                {
                    return (Loaded: false, Strokes: new List<InkStrokeData>());
                }

                var clonedStrokes = CloneInkStrokes(loaded);
                var loadedHash = ComputeInkHash(clonedStrokes);
                var runtimeStateKnown = _inkDirtyPages.TryGetRuntimeState(
                    sourcePath,
                    pageIndex,
                    out _,
                    out var runtimeHash,
                    out var runtimeDirty);
                if (!InkSidecarLoadAdmissionPolicy.ShouldApplyLoadedSnapshot(
                        runtimeStateKnown,
                        runtimeHash,
                        runtimeDirty,
                        loadedHash))
                {
                    var rejectedCacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, IsPdfFile(sourcePath));
                    if (!string.IsNullOrWhiteSpace(rejectedCacheKey))
                    {
                        _photoCache.Remove(rejectedCacheKey);
                        InvalidateNeighborInkCache(rejectedCacheKey);
                    }

                    System.Diagnostics.Debug.WriteLine(
                        $"[InkPersist] Skip sidecar snapshot due runtime conflict: source={sourcePath}, page={pageIndex}, runtimeHash={runtimeHash}, loadedHash={loadedHash}, dirty={runtimeDirty}");
                    return (Loaded: false, Strokes: new List<InkStrokeData>());
                }

                var cacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, IsPdfFile(sourcePath));
                if (!string.IsNullOrWhiteSpace(cacheKey))
                {
                    _photoCache.Set(cacheKey, clonedStrokes);
                }

                MarkInkPageLoaded(sourcePath, pageIndex, clonedStrokes);
                return (Loaded: true, Strokes: clonedStrokes);
            },
            fallback: (Loaded: false, Strokes: new List<InkStrokeData>()),
            onFailure: ex =>
            {
                System.Diagnostics.Debug.WriteLine($"[InkPersist] Load page failed: source={sourcePath}, page={pageIndex}, error={ex.Message}");
            });
        if (!loadResult.Loaded)
        {
            return false;
        }

        strokes = loadResult.Strokes;
        return true;
    }

    private bool TryLoadNeighborInkFromSidecarIntoCache(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return TryLoadSidecarPageInkToCache(_currentDocumentPath, pageIndex, out _);
        }

        var sequenceIndex = pageIndex - 1;
        if (sequenceIndex < 0 || sequenceIndex >= _photoSequencePaths.Count)
        {
            return false;
        }

        var sourcePath = _photoSequencePaths[sequenceIndex];
        return TryLoadSidecarPageInkToCache(sourcePath, 1, out _);
    }

    private static JsonSerializerOptions CreateInkHistoryJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private void PersistInkHistorySnapshot(
        string sourcePath,
        int pageIndex,
        List<InkStrokeData> strokes,
        InkPersistenceService persistence)
    {
        var historyAdapter = _inkHistorySnapshotStore;
        if (historyAdapter == null)
        {
            persistence.SaveInkForFile(sourcePath, pageIndex, strokes);
            return;
        }

        var strokesJson = SerializeInkStrokes(strokes);
        historyAdapter.Save(sourcePath, pageIndex, strokesJson);
    }

    private List<InkStrokeData> LoadInkHistorySnapshot(
        string sourcePath,
        int pageIndex,
        InkPersistenceService persistence)
    {
        var historyAdapter = _inkHistorySnapshotStore;
        if (historyAdapter == null)
        {
            return persistence.LoadInkPageForFile(sourcePath, pageIndex) ?? new List<InkStrokeData>();
        }

        var result = historyAdapter.LoadOrCreate(sourcePath, pageIndex, writeSnapshot: _inkSaveEnabled);
        return DeserializeInkStrokes(result.StrokesJson);
    }

    private static string? SerializeInkStrokes(List<InkStrokeData> strokes)
    {
        if (strokes == null || strokes.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(strokes, InkHistoryJsonOptions);
    }

    private static List<InkStrokeData> DeserializeInkStrokes(string? strokesJson)
    {
        if (string.IsNullOrWhiteSpace(strokesJson))
        {
            return new List<InkStrokeData>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<InkStrokeData>>(strokesJson, InkHistoryJsonOptions) ?? new List<InkStrokeData>();
        }
        catch (JsonException)
        {
            return new List<InkStrokeData>();
        }
    }
}
