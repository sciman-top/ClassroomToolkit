using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassroomToolkit.App.Ink;

public sealed class InkHistoryPersistenceBridge : ClassroomToolkit.Infra.Storage.IInkHistoryStoreBridge
{
    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();
    private readonly InkPersistenceService _persistence;

    public InkHistoryPersistenceBridge(InkPersistenceService persistence)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public ClassroomToolkit.Infra.Storage.InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex)
    {
        var strokes = _persistence.LoadInkPageForFile(sourcePath, pageIndex);
        var strokesJson = (strokes == null || strokes.Count == 0)
            ? null
            : JsonSerializer.Serialize(strokes, JsonOptions);
        var createdTemplate = string.IsNullOrWhiteSpace(strokesJson);
        return new ClassroomToolkit.Infra.Storage.InkHistoryLoadResult(sourcePath, pageIndex, strokesJson, createdTemplate);
    }

    public void Save(string sourcePath, int pageIndex, string? strokesJson)
    {
        List<InkStrokeData> strokes;
        if (string.IsNullOrWhiteSpace(strokesJson))
        {
            strokes = new List<InkStrokeData>();
        }
        else
        {
            strokes = JsonSerializer.Deserialize<List<InkStrokeData>>(strokesJson, JsonOptions) ?? new List<InkStrokeData>();
        }

        _persistence.SaveInkForFile(sourcePath, pageIndex, strokes);
    }

    private static JsonSerializerOptions BuildJsonOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
