using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClassroomToolkit.Application.Abstractions;

namespace ClassroomToolkit.App.Ink;

internal sealed class InkHistoryPersistenceBridge : IInkHistoryStoreBridge
{
    private static readonly JsonSerializerOptions JsonOptions = BuildJsonOptions();
    private readonly InkPersistenceService _persistence;

    public InkHistoryPersistenceBridge(InkPersistenceService persistence)
    {
        _persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public InkHistoryLoadResult LoadOrCreate(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return new InkHistoryLoadResult(sourcePath, pageIndex, null, CreatedTemplate: true);
        }

        var document = _persistence.LoadInkForFile(sourcePath);
        var page = document?.Pages?.FirstOrDefault(candidate => candidate.PageIndex == pageIndex);
        var strokes = page?.Strokes;
        var strokesJson = (strokes == null || strokes.Count == 0)
            ? null
            : JsonSerializer.Serialize(strokes, JsonOptions);
        var createdTemplate = string.IsNullOrWhiteSpace(strokesJson);
        var updatedAtUtc = string.IsNullOrWhiteSpace(strokesJson)
            ? null
            : page?.UpdatedAt.ToUniversalTime();
        return new InkHistoryLoadResult(sourcePath, pageIndex, strokesJson, createdTemplate, updatedAtUtc);
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
