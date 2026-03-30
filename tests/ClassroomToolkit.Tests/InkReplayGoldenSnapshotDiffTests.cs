using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkReplayGoldenSnapshotDiffTests
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    static InkReplayGoldenSnapshotDiffTests()
    {
        SnapshotJsonOptions.Converters.Add(new JsonStringEnumConverter());
    }

    [Fact]
    public async Task GeneratedSnapshots_ShouldMatchCurrentGoldenBaselines()
    {
        var recipes = InkReplayGoldenRecipeCatalog.GetAll()
            .ToDictionary(x => x.FileName, x => x.ExecuteAsync, StringComparer.OrdinalIgnoreCase);
        var actionDir = TestPathHelper.ResolveRepoPath("tests", "Baselines", "ink-replay-actions");
        var baselineFiles = Directory.GetFiles(actionDir, "*.actions.json", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        recipes.Keys.Should().BeEquivalentTo(
            baselineFiles,
            "every golden baseline should have a snapshot generation recipe");

        foreach (var fileName in baselineFiles)
        {
            var expected = NormalizeBaselineJson(File.ReadAllText(Path.Combine(actionDir, fileName!)));
            var actualActions = await recipes[fileName!]();
            var generated = NormalizeBaselineJson(
                JsonSerializer.Serialize(
                    new InkReplayGoldenActions(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(fileName!)), actualActions),
                    SnapshotJsonOptions));

            generated.Should().Be(
                expected,
                $"{fileName}: generated snapshot drifted from current baseline");
        }
    }

    private static string NormalizeBaselineJson(string json)
    {
        var model = JsonSerializer.Deserialize<InkReplayGoldenActions>(json, SnapshotJsonOptions);
        model.Should().NotBeNull();
        return JsonSerializer.Serialize(model, SnapshotJsonOptions);
    }
}
