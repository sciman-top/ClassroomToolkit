using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClassroomToolkit.Tests;

public enum InkReplayEventType
{
    PointerDown = 0,
    PointerMove = 1,
    PointerUp = 2,
    PhotoPanStart = 3,
    PhotoPanMove = 4,
    PhotoPanEnd = 5,
    CrossPageSwitch = 6
}

public sealed record InkReplayEventSample(
    InkReplayEventType Type,
    long TimestampMs,
    double X,
    double Y,
    double? Pressure = null,
    int? PageIndex = null);

public sealed record InkReplayScenario(
    string Name,
    string Description,
    IReadOnlyList<InkReplayEventSample> Events);

public static class InkReplayFixtureCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    static InkReplayFixtureCatalog()
    {
        JsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    public static InkReplayScenario Load(string scenarioFileName)
    {
        if (string.IsNullOrWhiteSpace(scenarioFileName))
        {
            throw new ArgumentException("Scenario file name is required.", nameof(scenarioFileName));
        }

        var path = TestPathHelper.ResolveRepoPath(
            "tests",
            "Baselines",
            "ink-replay",
            scenarioFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Replay scenario file not found.", path);
        }

        var json = File.ReadAllText(path);
        var scenario = JsonSerializer.Deserialize<InkReplayScenario>(json, JsonOptions);
        if (scenario == null)
        {
            throw new InvalidDataException($"Failed to deserialize replay scenario: {path}");
        }

        return scenario;
    }
}
