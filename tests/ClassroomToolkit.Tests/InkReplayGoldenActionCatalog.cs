using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassroomToolkit.Tests;

public sealed record InkReplayGoldenActions(
    string Name,
    IReadOnlyList<InkReplayAction> Actions);

public static class InkReplayGoldenActionCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static InkReplayGoldenActionCatalog()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static InkReplayGoldenActions Load(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("Golden action file name is required.", nameof(fileName));
        }

        var path = TestPathHelper.ResolveRepoPath(
            "tests",
            "Baselines",
            "ink-replay-actions",
            fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Golden action file not found.", path);
        }

        var json = File.ReadAllText(path);
        var value = JsonSerializer.Deserialize<InkReplayGoldenActions>(json, JsonOptions);
        if (value == null)
        {
            throw new InvalidDataException($"Failed to deserialize golden actions: {path}");
        }

        return value;
    }
}
